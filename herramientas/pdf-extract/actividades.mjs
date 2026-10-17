// Extrae las operaciones (compras y ventas) de las secciones de actividad de los estados de cuenta.
//
// Uso:  node actividades.mjs <carpeta-textos> <csv-salida>
//
// Formato de fila (Questrade), en frances o en ingles:
//   MM-DD-YYYY  MM-DD-YYYY  Acheter|Vendre|Buy|Sell  SIMBOLO [descripcion]  Cant | CAD(Prix,Bruto,Com,Net) | USD(Prix,Bruto,Com,Net)
//
// Detalles que hay que respetar, aprendidos a base de fallos:
//   - Las fechas vienen en formato MM-DD-YYYY, no DD-MM.
//   - Los importes negativos van entre parentesis: (1 780,00) = -1780,00
//   - El frances usa ESPACIO de millares (3 168,00) y coma decimal; el ingles usa coma de millares
//     y punto decimal (5,136.90). Son patrones NUMERICOS DISTINTOS y hay que elegirlos por idioma,
//     porque "288,000" es 288.000 en frances y 288000 en ingles.
//   - La descripcion puede ir pegada en la misma linea que el simbolo ("VEEV AVONS AGI COMME AGENT
//     PRIX MOYEN- 7,0000 ..."), asi que el bloque numerico se localiza por la CANTIDAD (4 decimales),
//     no troceando por espacios: asi el guion de "MOYEN-" no se confunde con un campo vacio.
//   - El mismo hecho aparece en dos estados: la seccion "operaciones liquidadas tras el cierre del
//     mes" del estado de marzo repite lo que el de abril lista como actividad normal. Hay que deduplicar.

import fs from 'node:fs';
import path from 'node:path';

const entrada = process.argv[2];
const csvSalida = process.argv[3];
if (!entrada || !csvSalida) {
  console.error('Uso: node actividades.mjs <carpeta-textos> <csv-salida>');
  process.exit(1);
}

function buscarTxt(dir, prefijo = '') {
  const encontrados = [];
  const entradas = fs.readdirSync(dir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name));
  for (const e of entradas) {
    const rel = prefijo ? path.join(prefijo, e.name) : e.name;
    const completo = path.join(dir, e.name);
    if (e.isDirectory()) encontrados.push(...buscarTxt(completo, rel));
    else if (e.name.toLowerCase().endsWith('.txt')) encontrados.push(rel);
  }
  return encontrados;
}

// Patrones de campo, distintos por idioma
const CAMPO_FR = String.raw`(?:\(?-?\d{1,3}(?:[ \u00a0\u202f]\d{3})*[.,]\d{1,4}\)?|-)`;
const CAMPO_EN = String.raw`(?:\(?-?\d{1,3}(?:,\d{3})*\.\d{1,4}\)?|-)`;

// La cantidad siempre lleva 4 decimales: sirve para localizar donde empieza el bloque numerico.
const RE_CANTIDAD = /\(?-?\d[\d,]*[.,]\d{4}\)?/;

const RE_OP = /^(\d{2})-(\d{2})-(\d{4})\s+(\d{2})-(\d{2})-(\d{4})\s+(Acheter|Vendre|Buy|Sell|DIV|[A-Za-zÀ-ÿ]{3,20})\s+(\S+)\s*(.*)$/;
const RE_CON_FECHA = /^(\d{2}-\d{2}-\d{4})/;

const aNumero = (s) => {
  if (s === undefined || s === null || s === '-') return null;
  let t = String(s).trim();
  let negativo = false;
  if (/^\(.*\)$/.test(t)) { negativo = true; t = t.slice(1, -1); }
  t = t.replace(/[\s\u00a0\u202f]/g, '');
  if (t.includes(',') && t.includes('.')) t = t.replace(/,/g, '');
  else if (t.includes(',')) t = t.replace(',', '.');
  const n = Number(t);
  if (!Number.isFinite(n)) return null;
  return negativo ? -n : n;
};

const RE_CUENTA = /(?:Account #|No de compte)\s*:?\s*(\d+)/i;
const MESES = { janvier: '01', 'février': '02', fevrier: '02', mars: '03', avril: '04', mai: '05', juin: '06',
  juillet: '07', 'août': '08', aout: '08', septembre: '09', octobre: '10', novembre: '11', 'décembre': '12', decembre: '12',
  january: '01', february: '02', march: '03', april: '04', may: '05', june: '06', july: '07', august: '08',
  september: '09', october: '10', november: '11', december: '12' };

function mesDe(texto) {
  const m = texto.match(/(?:Current month|Mois courant)\s*:?\s*([A-Za-zéûôà]+)\s+(\d{1,2}),?\s+(\d{4})/i);
  if (m && MESES[m[1].toLowerCase()]) return `${m[3]}-${MESES[m[1].toLowerCase()]}`;
  return '';
}

const archivos = buscarTxt(entrada);
const operaciones = [];
const otrosTipos = new Map();
const noParseadas = [];

for (const rel of archivos) {
  const texto = fs.readFileSync(path.join(entrada, rel), 'utf8');
  const mCuenta = texto.match(RE_CUENTA);
  const cuenta = mCuenta ? mCuenta[1] : '';
  const mes = mesDe(texto);
  const esFrances = /Acheter|Vendre|Mois courant|No de compte/.test(texto);
  const RE_CAMPOS = new RegExp(esFrances ? CAMPO_FR : CAMPO_EN, 'g');

  for (const linea of texto.split(/\r?\n/)) {
    if (!RE_CON_FECHA.test(linea)) continue;
    const m = linea.match(RE_OP);
    if (!m) continue;

    const [, mes1, dia1, anio1, mes2, dia2, anio2, tipo, simboloBruto, resto] = m;
    const esOperacion = /^(Acheter|Vendre|Buy|Sell)$/i.test(tipo);

    // El bloque numerico empieza donde aparece la cantidad (4 decimales).
    const mCant = resto.match(RE_CANTIDAD);
    if (!mCant) {
      if (esOperacion) noParseadas.push(`${rel}: ${linea.slice(0, 150)}`);
      continue;
    }
    const campos = [...resto.slice(mCant.index).matchAll(RE_CAMPOS)].map((x) => x[0]);

    if (!esOperacion) {
      otrosTipos.set(tipo, (otrosTipos.get(tipo) || 0) + 1);
      continue;
    }

    const cantidad = aNumero(campos[0]);
    const cad = { precio: aNumero(campos[1]), bruto: aNumero(campos[2]), comision: aNumero(campos[3]), neto: aNumero(campos[4]) };
    const usd = { precio: aNumero(campos[5]), bruto: aNumero(campos[6]), comision: aNumero(campos[7]), neto: aNumero(campos[8]) };

    let moneda = '';
    let bloque = null;
    if (usd.precio !== null) { moneda = 'USD'; bloque = usd; }
    else if (cad.precio !== null) { moneda = 'CAD'; bloque = cad; }

    const signo = /^(Vendre|Sell)$/i.test(tipo) ? -1 : 1;

    operaciones.push({
      archivo: rel, mes, cuenta,
      fecha: `${anio1}-${mes1}-${dia1}`,
      fechaLiquidacion: `${anio2}-${mes2}-${dia2}`,
      tipo: signo === 1 ? 'Compra' : 'Venta',
      simbolo: simboloBruto.replace(/^\./, ''),
      cantidad: cantidad === null ? null : signo * Math.abs(cantidad),
      moneda, precio: bloque ? bloque.precio : null, bruto: bloque ? bloque.bruto : null,
      comision: bloque ? bloque.comision : null, neto: bloque ? bloque.neto : null,
    });
  }
}

// Deduplicar: el mismo hecho aparece en dos estados consecutivos.
const vistas = new Map();
const unicas = [];
for (const o of operaciones.sort((a, b) => (a.cuenta + a.fecha + a.simbolo).localeCompare(b.cuenta + b.fecha + b.simbolo))) {
  const clave = [o.cuenta, o.fecha, o.tipo, o.simbolo, o.cantidad, o.precio].join('|');
  if (vistas.has(clave)) { vistas.get(clave).repetidaEn.push(o.mes); continue; }
  o.repetidaEn = [];
  vistas.set(clave, o);
  unicas.push(o);
}

const escapar = (v) => {
  const s = v === null || v === undefined ? '' : String(v);
  return /[",;\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
};
const cab = ['Cuenta', 'Mes', 'Fecha', 'FechaLiquidacion', 'Tipo', 'Simbolo', 'Cantidad', 'Moneda',
  'Precio', 'Bruto', 'Comision', 'Neto', 'Archivo'];
const out = [cab.join(',')];
for (const o of unicas) {
  out.push([o.cuenta, o.mes, o.fecha, o.fechaLiquidacion, o.tipo, o.simbolo, o.cantidad, o.moneda,
    o.precio, o.bruto, o.comision, o.neto, o.archivo].map(escapar).join(','));
}
fs.mkdirSync(path.dirname(csvSalida), { recursive: true });
fs.writeFileSync(csvSalida, out.join('\n'), 'utf8');

console.log(`Lineas de operacion leidas: ${operaciones.length}   tras deduplicar: ${unicas.length}`);
console.log(`  compras: ${unicas.filter((o) => o.tipo === 'Compra').length}   ventas: ${unicas.filter((o) => o.tipo === 'Venta').length}`);
console.log(`  en USD: ${unicas.filter((o) => o.moneda === 'USD').length}   en CAD: ${unicas.filter((o) => o.moneda === 'CAD').length}   sin moneda: ${unicas.filter((o) => !o.moneda).length}`);
if (otrosTipos.size) {
  console.log(`  otras lineas de actividad (fuera de alcance): ${[...otrosTipos.entries()].map(([k, v]) => `${k} x${v}`).join(', ')}`);
}
console.log('');
console.log('DETALLE');
for (const o of unicas) {
  const rep = o.repetidaEn.length ? `  (repetida en ${o.repetidaEn.join(', ')})` : '';
  console.log(`  ${o.cuenta} ${o.fecha} ${o.tipo.padEnd(6)} ${o.simbolo.padEnd(8)} ${String(o.cantidad).padStart(10)} ${o.moneda} @ ${String(o.precio).padStart(11)}   neto ${String(o.neto).padStart(11)}${rep}`);
}
if (noParseadas.length) {
  console.log('');
  console.log(`NO PARSEADAS (${noParseadas.length}):`);
  noParseadas.forEach((n) => console.log('  ' + n));
}
console.log('');
console.log(`CSV: ${csvSalida}`);
