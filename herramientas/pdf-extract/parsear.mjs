// Extrae las posiciones de los estados de cuenta y genera el listado consolidado.
//
// Uso:  node parsear.mjs <carpeta-textos> <carpeta-salida>
//
// Formatos soportados:
//   A) Questrade (SSRS) en ingles y en frances.
//      SYMBOL DESCRIPTION [BK|HMV|HMVD|ND] Qty Segr Cost/share Pos.cost Mkt.price Mkt.value P&L %return %port
//   B) TD Waterhouse (Portefeuille gere) en frances.
//      DESCRIPTION  Cantidad SEG Precio Cout Valeur %
//
// Notas de formato:
//   - Questrade EN: millares con coma, decimal con punto  (2,095.00)
//   - Questrade FR: millares con espacio o coma; cantidad y % cartera con COMA decimal (100,0000 / 16,02),
//     pero importes y % rendimiento con PUNTO decimal (11,720.25 / -0.99)
//   - TD FR: millares con espacio, decimal con coma  (39 679,36)
//
// Moneda: las cifras de la seccion "en USD" estan EN USD. No se pueden sumar filas de distinta moneda.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const entrada = process.argv[2];
const salida = process.argv[3];
if (!entrada || !salida) {
  console.error('Uso: node parsear.mjs <carpeta-textos> <carpeta-salida>');
  process.exit(1);
}

// ---------------------------------------------------------------- utilidades

const aNumero = (s) => {
  let t = String(s).replace(/[\s\u00a0\u202f]/g, '');
  if (t.includes(',') && t.includes('.')) t = t.replace(/,/g, '');
  else if (t.includes(',')) t = t.replace(',', '.');
  return Number(t);
};

// ---------------------------------------------------------------- fechas

const MESES = {
  janvier: '01', fevrier: '02', 'février': '02', mars: '03', avril: '04', mai: '05', juin: '06',
  juillet: '07', aout: '08', 'août': '08', septembre: '09', octobre: '10', novembre: '11',
  decembre: '12', 'décembre': '12',
  january: '01', february: '02', march: '03', april: '04', may: '05', june: '06', july: '07',
  august: '08', september: '09', october: '10', november: '11', december: '12',
};

function aFecha(texto) {
  const arm = (anio, mes, dia) => {
    const mm = MESES[String(mes).toLowerCase()];
    return mm ? `${anio}-${mm}-${String(dia).padStart(2, '0')}` : '';
  };
  let m = texto.match(/(?:Current month|Mois courant)\s*:?\s*([A-Za-zéûôà]+)\s+(\d{1,2}),?\s+(\d{4})/i);
  if (m) { const r = arm(m[3], m[1], m[2]); if (r) return r; }
  m = texto.match(/(?:Current month|Mois courant)\s*:?\s*(\d{1,2})\s+([A-Za-zéûôà]+),?\s+(\d{4})/i);
  if (m) { const r = arm(m[3], m[2], m[1]); if (r) return r; }
  // TD: "1 janvier 2026 au 31 janvier 2026"  -> nos quedamos con el final del periodo
  m = texto.match(/\d{1,2}\s+[A-Za-zéûôà]+\.?\s+\d{4}\s+au\s+(\d{1,2})\s+([A-Za-zéûôà]+)\.?\s+(\d{4})/i);
  if (m) { const r = arm(m[3], m[2], m[1]); if (r) return r; }
  return '';
}

// ---------------------------------------------------------------- regex

const N4 = '-?[\\d,]+[,.]\\d{4}';      // cantidad 4 decimales (Questrade)
const N2 = '-?[\\d,]*\\d\\.\\d{2}';    // importe con punto decimal (Questrade)
const P2 = '-?[\\d,]*\\d[,.]\\d{2}';   // porcentaje (Questrade)

const RE_FILA_QUESTRADE = new RegExp(
  '^([A-Z][A-Z0-9.\\-]{0,9})\\s+(.*?)\\s+' +
  '(BK|HMV|HMVD|ND)\\s+' +
  `(${N4})\\s+(${N4})\\s+(${N2})\\s+(${N2})\\s+(${N2})\\s+(${N2})\\s+(${N2})\\s+(${N2})\\s+(${P2})`
);

// TD: "DESC  2 393,827 SEG 19,840 39 679,36 47 493,52 52,84%"
const RE_FILA_TD = /(?:^|\t)\s*((?:[\d\u00a0\u202f]| )*[\d\u00a0\u202f],\d{1,4})\s+SEG\s+([\d\u00a0\u202f ]+,\d{1,4})\s+([\d\u00a0\u202f ]+,\d{2})\s+([\d\u00a0\u202f ]+,\d{2})\s+([\d\u00a0\u202f ]+,\d{2})%/;

const RE_SECCION = /^(?:Securities held in|Titres d[ée]tenus en)\s+(CAD|USD)/i;
const RE_TOTALES = /^(?:Market Value \(\$\)|Cours boursier \(\$\))\s*(.*)$/i;
const RE_CUENTA = /(?:Account #|No de compte|Numéro de compte)\s*:?\s*([0-9A-Z\-]+)/i;
const RE_MES = /(?:Current month|Mois courant)\s*:?\s*([A-Za-zéû]+\s+\d{1,2},?\s+\d{4})/i;
const RE_CABECERA = /(?:Symbol Description|Symbole Description)/i;
const RE_CODIGO_FONDO = /\(([A-Z]{2,4}\d{4}[A-Z]?)\)/;

// Nombres de titular: linea completa o tras tabulador, en mayusculas, 3-5 palabras.
const RE_NOMBRE = /(?:^|\t)\s*((?:[A-ZÁÉÍÓÚÑ]{2,}\s+){2,4}[A-ZÁÉÍÓÚÑ]{2,})\s*(?:\t|$)/;
const PALABRAS_NO_NOMBRE = new Set([
  'COMPTE', 'ETAT', 'ÉTAT', 'GESTION', 'PLACEMENT', 'PLACEMENTS', 'INC', 'LTD', 'TRADES', 'SETTLE', 'AFTER',
  'INVESTMENT', 'DETAILS', 'RENSEIGNEMENTS', 'ACCOUNT', 'STATEMENT', 'WATERHOUSE', 'QUESTRADE', 'SOMMAIRE',
  'CONTENU', 'REVENU', 'FIXE', 'ACTIONS', 'FONDS', 'MUTUELS', 'CANADIENS', 'ÉTRANGÈRES', 'ESPECES', 'ESPÈCES',
  'DISPONIBLE', 'TITRES', 'TOTAL', 'PAGE', 'MEMBRE', 'PROTECTION', 'EPARGNANTS', 'ÉPARGNANTS', 'DROITS',
  'RESERVES', 'RÉSERVÉS', 'MARQUE', 'ENREGISTREE', 'ENREGISTRÉE', 'DE', 'TYPE', 'DEVISE', 'COURTIER',
  'OUVERTURE', 'DATE', 'PORTEFEUILLE', 'RENDEMENT', 'OPERATIONS', 'OPÉRATIONS', 'PRETS', 'PRÊTS', 'GARANTIES',
  'LIBERES', 'LIBÉRÉS', 'APERCU', 'APERÇU', 'VOTRE', 'RELEVE', 'RELEVÉ', 'DU', 'DES', 'LES', 'UNE', 'VOS',
]);

function detectarTitular(lineas, indiceCuenta) {
  const desde = Math.max(0, indiceCuenta - 8);
  const hasta = Math.min(lineas.length - 1, indiceCuenta + 1);
  const candidatos = [];
  for (let i = desde; i <= hasta; i++) {
    const m = lineas[i].match(RE_NOMBRE);
    if (!m) continue;
    const palabras = m[1].trim().split(/\s+/);
    const malas = palabras.filter((p) => PALABRAS_NO_NOMBRE.has(p.replace(/[^A-ZÁÉÍÓÚÑ]/g, '')));
    if (malas.length === 0 && palabras.length >= 3 && palabras.length <= 5) {
      candidatos.push({ nombre: palabras.join(' '), distancia: Math.abs(i - indiceCuenta), i });
    }
  }
  candidatos.sort((a, b) => a.distancia - b.distancia);
  return candidatos.length ? candidatos[0].nombre : '';
}

// ---------------------------------------------------------------- recorrido

// Recorre subcarpetas y devuelve rutas relativas, para poder leer varias cuentas a la vez.
function buscarTxt(dir, prefijo = '') {
  const encontrados = [];
  const entradas = fs.readdirSync(dir, { withFileTypes: true })
    .sort((a, b) => a.name.localeCompare(b.name));
  for (const e of entradas) {
    const rel = prefijo ? path.join(prefijo, e.name) : e.name;
    const completo = path.join(dir, e.name);
    if (e.isDirectory()) encontrados.push(...buscarTxt(completo, rel));
    else if (e.name.toLowerCase().endsWith('.txt')) encontrados.push(rel);
  }
  return encontrados;
}

const archivos = buscarTxt(entrada);
const filas = [];
const incidencias = [];
const validaciones = [];

for (const archivo of archivos) {
  const texto = fs.readFileSync(path.join(entrada, archivo), 'utf8');
  const lineas = texto.split(/\r?\n/);

  const esTD = /Gestion de patrimoine TD|Portefeuille géré/i.test(texto);
  const esFrances = /Titres d[ée]tenus|Mois courant|No de compte|Cours boursier/i.test(texto);

  const mCuenta = texto.match(RE_CUENTA);
  const cuenta = mCuenta ? mCuenta[1] : '';
  const mNombre = texto.match(/M ([A-ZÁÉÍÓÚÑ]{2,}(?: [A-ZÁÉÍÓÚÑ]{2,}){2,4})/);
  const iCuenta = lineas.findIndex((l) => RE_CUENTA.test(l));
  let titular = detectarTitular(lineas, iCuenta >= 0 ? iCuenta : 15);
  if (!titular && mNombre) titular = mNombre[1];

  const fecha = aFecha(texto);

  // Tipo de cuenta: primero por el nombre del archivo, y si no, por el contenido.
  let tipo = /TFSA/i.test(archivo) ? 'TFSA' : /RRSP|REER/i.test(archivo) ? 'RRSP' : 'OTRO';
  if (tipo === 'OTRO') {
    if (/TFSA|C[ÉE]LI|libre d'imp[ôo]t/i.test(texto)) tipo = 'TFSA';
    else if (/RRSP|REER|R[ÉE]R\b|[ée]pargne-retraite/i.test(texto)) tipo = 'RRSP';
  }
  const bruto = esTD ? 'TD' : 'Questrade';

  // --- Totales declarados (para autovalidacion)
  const totales = [];
  for (let i = 0; i < lineas.length; i++) {
    const m = lineas[i].match(RE_TOTALES);
    if (!m) continue;
    let cuerpo = m[1];
    if (!/\d/.test(cuerpo) && lineas[i + 1]) cuerpo = lineas[i + 1]; // el numero puede ir en la linea siguiente
    const patron = esFrances ? /-?\d[\d\s\u00a0\u202f]*,\d{2}/g : /-?\d[\d,]*\.\d{2}/g;
    const nums = [...cuerpo.matchAll(patron)].map((x) => aNumero(x[0]));
    const pagina = Number((lineas.slice(0, i).reverse().find((l) => /^===== PAGINA/.test(l)) || '').replace(/\D/g, '')) || 0;
    if (nums.length) totales.push({ pagina, nums });
  }

  if (esTD) {
    // ---------------- Formato TD
    for (let i = 0; i < lineas.length; i++) {
      const m = lineas[i].match(RE_FILA_TD);
      if (!m) continue;
      const [, cant, precio, coste, valor, pct] = m;

      // Codigo de fondo: en la propia linea o en las dos siguientes
      let codigo = '';
      for (let k = i; k <= i + 2 && k < lineas.length; k++) {
        const mc = lineas[k].match(RE_CODIGO_FONDO);
        if (mc) { codigo = mc[1]; break; }
      }

      // Descripcion: texto antes de las cifras, o la linea anterior/siguiente que no sea un total
      let descripcion = lineas[i].slice(0, m.index).trim().replace(/\t/g, ' ');
      if (!descripcion || descripcion.length < 3) {
        for (let k = i - 1; k <= i + 2 && k < lineas.length; k++) {
          if (k < 0 || k === i) continue;
          const l = lineas[k].trim();
          if (/^(Total|Encaisse|\()/.test(l)) continue;
          if (/^[A-Z0-9][A-Z0-9 .\-\/]{4,}$/.test(l)) { descripcion = l; break; }
        }
      }

      filas.push({
        archivo, bruto, cuenta, titular, tipo, fecha, pagina: 0, moneda: 'CAD',
        simbolo: codigo, descripcion: descripcion.replace(/\s+/g, ' ').trim(), base: 'SEG',
        cantidad: aNumero(cant), costeUnitario: aNumero(coste) / aNumero(cant), costePosicion: aNumero(coste),
        precioMercado: aNumero(precio), valorMercado: aNumero(valor), ganancia: aNumero(valor) - aNumero(coste),
        pctRetorno: NaN, pctCartera: aNumero(pct),
      });
    }
  } else {
    // ---------------- Formato Questrade
    let pagina = 0;
    let seccionActual = null;
    const secciones = [];
    for (const linea of lineas) {
      const mPag = linea.match(/^===== PAGINA (\d+)/);
      if (mPag) { pagina = Number(mPag[1]); continue; }
      if (RE_CABECERA.test(linea)) continue;

      const mSec = linea.match(RE_SECCION);
      if (mSec) {
        seccionActual = { pagina, moneda: mSec[1].toUpperCase(), suma: 0, filas: 0 };
        secciones.push(seccionActual);
        continue;
      }

      const m = linea.match(RE_FILA_QUESTRADE);
      if (m) {
        const [, simbolo, descripcion, base, qty, segr, costeUd, costePos, precio, valor, pnl, pctRet, pctPort] = m;
        filas.push({
          archivo, bruto, cuenta, titular, tipo, fecha, pagina,
          moneda: seccionActual ? seccionActual.moneda : 'CAD',
          simbolo, descripcion: descripcion.trim(), base,
          cantidad: aNumero(qty), segregado: aNumero(segr),
        costeUnitario: aNumero(costeUd), costePosicion: aNumero(costePos),
          precioMercado: aNumero(precio), valorMercado: aNumero(valor), ganancia: aNumero(pnl),
          pctRetorno: aNumero(pctRet), pctCartera: aNumero(pctPort),
        });
        if (seccionActual) { seccionActual.suma += aNumero(valor); seccionActual.filas++; }
        continue;
      }

      if (/\b(BK|HMV|HMVD|ND)\b/.test(linea) && /\d/.test(linea) && /^[A-Z][A-Z0-9.\-]{0,9}\s/.test(linea)) {
        incidencias.push(`SIN PARSEAR ${archivo} pag ${pagina}: ${linea.slice(0, 140)}`);
      }
    }

    for (const s of secciones) {
      if (!s.filas) continue;
      const cand = totales.filter((t) => t.pagina === s.pagina || t.pagina === s.pagina - 1);
      const ok = cand.some((t) => t.nums.some((n) => Math.abs(n - s.suma) < 0.05));
      validaciones.push({ archivo, pagina: s.pagina, moneda: s.moneda, suma: s.suma, ok });
      if (!ok) incidencias.push(`SIN CUADRAR ${archivo} pag ${s.pagina} ${s.moneda}: extraido ${s.suma.toFixed(2)} | declarado ${cand.map((c) => '[' + c.nums.join(' ') + ']').join(' ') || 'n/d'}`);
    }
  }

  if (!titular) incidencias.push(`TITULAR NO DETECTADO en ${archivo}`);
}

// ---------------------------------------------------------------- consolidado: foto mas reciente por cuenta

const porCuenta = new Map();
for (const f of filas) {
  const clave = `${f.bruto}|${f.cuenta}`;
  if (!porCuenta.has(clave)) porCuenta.set(clave, []);
  porCuenta.get(clave).push(f);
}

const recientes = [];
for (const [, lista] of porCuenta) {
  const fechas = lista.map((f) => f.fecha).filter(Boolean).sort();
  const ultima = fechas[fechas.length - 1];
  lista.filter((f) => f.fecha === ultima).forEach((f) => recientes.push(f));
}

// ---------------------------------------------------------------------------------------------
// Mapa codigo de fondo -> simbolo en la fuente de precios.
//
// Hace falta porque la fuente NO indexa por codigo Fundserv: buscar "TDB2624" devuelve CERO
// resultados, y en Canada un fondo mutuo de Fundserv no tiene ISIN. La equivalencia se resuelve a
// mano UNA vez por instrumento: se busca por nombre, y se acepta el candidato solo si su NAV
// coincide con el precio que imprime el estado de cuenta. Es la unica forma de distinguir series
// con nombres casi identicos y precios distintos.
// ---------------------------------------------------------------------------------------------
const aqui = path.dirname(fileURLToPath(import.meta.url));
const mapaInstrumentos = new Map();
const rutaMapa = path.join(aqui, 'mapa-instrumentos.csv');
if (fs.existsSync(rutaMapa)) {
  for (const linea of fs.readFileSync(rutaMapa, 'utf8').split(/\r?\n/).slice(1)) {
    if (!linea.trim()) continue;
    const c = linea.split(',').map((x) => x.trim().replace(/^"|"$/g, ''));
    if (c[0] && c[2]) mapaInstrumentos.set(c[0], { nombre: c[1], yahoo: c[2], verificado: c[3] || '' });
  }
}

// Simbolo para la fuente de precios: primero el mapa resuelto, luego una propuesta por convencion.
function simboloPropuesto(f) {
  const resuelto = mapaInstrumentos.get(f.simbolo);
  if (resuelto) return resuelto.yahoo;
  if (f.bruto === 'TD' || !f.simbolo) return '';   // fondo sin resolver: hay que buscarlo por nombre
  if (f.moneda === 'CAD') return f.simbolo.replace(/\./g, '-') + '.TO';
  return f.simbolo;
}

const escapar = (v) => {
  const s = v === null || v === undefined || Number.isNaN(v) ? '' : String(v);
  return /[",;\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
};
const f2 = (n) => (Number.isFinite(n) ? n.toFixed(2) : '');
const f4 = (n) => (Number.isFinite(n) ? n.toFixed(4) : '');

const cabecera = ['Titular', 'Corredor', 'TipoCuenta', 'Cuenta', 'FechaEstado', 'Moneda', 'Simbolo',
  'Descripcion', 'SimboloPropuesto', 'Cantidad', 'Segregado', 'CosteUnitario', 'CosteTotal',
  'PrecioMercado', 'ValorMercado', 'Ganancia', 'PctRetorno', 'PctCartera'];

function aCsv(lista, ruta) {
  const out = [cabecera.join(',')];
  for (const f of lista) {
    out.push([
      f.titular, f.bruto, f.tipo, f.cuenta, f.fecha, f.moneda, f.simbolo,
      f.descripcion, simboloPropuesto(f), f.cantidad, f.segregado, f2(f.costeUnitario),
      f2(f.costePosicion), f2(f.precioMercado), f2(f.valorMercado), f2(f.ganancia),
      f2(f.pctRetorno), f2(f.pctCartera),
    ].map(escapar).join(','));
  }
  fs.mkdirSync(path.dirname(ruta), { recursive: true });
  fs.writeFileSync(ruta, out.join('\n'), 'utf8');
}

aCsv(filas, path.join(salida, 'posiciones-todos-los-estados.csv'));
aCsv(recientes, path.join(salida, 'listado.csv'));

// ---------------------------------------------------------------- informe

const okTotales = validaciones.filter((v) => v.ok).length;
console.log(`Secciones validadas contra el total del estado: ${okTotales}/${validaciones.length}`);
console.log('');
console.log('CUENTAS Y TITULARES');
for (const [clave, lista] of [...porCuenta.entries()].sort()) {
  const f = lista[0];
  const fechas = [...new Set(lista.map((x) => x.fecha))].sort();
  console.log(`  ${(f.titular || 'SIN TITULAR').padEnd(30)} ${f.bruto.padEnd(10)} ${f.tipo.padEnd(5)} ${f.cuenta.padEnd(10)} ${lista.length} pos.  estados: ${fechas.join(', ')}`);
}
console.log('');
console.log(`LISTADO CONSOLIDADO (foto mas reciente de cada cuenta): ${recientes.length} posiciones`);
const porMoneda = new Map();
for (const f of recientes) {
  const k = f.moneda;
  if (!porMoneda.has(k)) porMoneda.set(k, { n: 0, valor: 0 });
  porMoneda.get(k).n++;
  porMoneda.get(k).valor += f.valorMercado;
}
for (const [m, d] of porMoneda) console.log(`  ${m}: ${d.n} posiciones, valor de mercado ${d.valor.toFixed(2)} ${m}`);

if (incidencias.length) {
  console.log('');
  console.log(`INCIDENCIAS (${incidencias.length}):`);
  incidencias.forEach((i) => console.log('  ' + i));
}
console.log('');
console.log(`CSV completo : ${path.join(salida, 'posiciones-todos-los-estados.csv')}`);
console.log(`CSV listado  : ${path.join(salida, 'listado.csv')}`);
