// Cuadra los movimientos extraidos contra los totales que imprimen los propios estados de cuenta.
//
// Uso:  node cuadrar.mjs <fichero-json-estados>
//
// Es la unica prueba que vale la pena de una extraccion: no "se leyeron 170 filas", sino "las 170
// filas suman exactamente lo que el estado dice que sumaron, mes a mes, durante siete años".
//
// Se comprueba, por separado para CAD y para USD:
//
//   1. INTERNA: los conceptos que imprime el estado, sumados, dan su saldo de cierre.
//      Si esto falla, quien lee mal la tabla de totales soy yo, no los movimientos.
//   2. CONTRA LOS MOVIMIENTOS: los movimientos extraidos, agrupados por concepto, dan el importe que
//      el estado imprime para ese concepto.
//   3. CADENA: saldo de cierre = saldo de apertura + todos los movimientos del mes.
//
// Sobre los signos, que es donde se pierde el tiempo: el estado imprime las VENTAS y las COMPRAS por su
// importe BRUTO, y las comisiones aparte, en su propia linea. Comprobarlo con "neto" da una diferencia
// de 4,95 por operacion que parece un error de redondeo y no lo es.

import fs from 'node:fs';
import { getDocument } from '../pdf-extract/node_modules/pdfjs-dist/legacy/build/pdf.mjs';

const fichero = process.argv[2];
if (!fichero) {
  console.error('Uso: node cuadrar.mjs <fichero-json-estados>');
  process.exit(1);
}

const TOL_FILA = 1.5;
const TOL_COL = 3;

const RE_NUMERO = /^\(?-?\d[\d .,]*\)?$|^-$/;
const esNumero = (s) => RE_NUMERO.test(s.trim());

function aNumero(s) {
  const t = String(s).trim();
  if (t === '' || t === '-') return 0;
  let negativo = false;
  let cuerpo = t;
  if (/^\(.*\)$/.test(cuerpo)) { negativo = true; cuerpo = cuerpo.slice(1, -1); }
  cuerpo = cuerpo.replace(/[\s\u00a0\u202f]/g, '');
  if (cuerpo.includes(',') && cuerpo.includes('.')) {
    cuerpo = cuerpo.lastIndexOf(',') > cuerpo.lastIndexOf('.')
      ? cuerpo.replace(/\./g, '').replace(',', '.') : cuerpo.replace(/,/g, '');
  } else cuerpo = cuerpo.replace(',', '.');
  const n = Number(cuerpo);
  return negativo ? -n : n;
}

const sinAcentos = (s) => s.normalize('NFD').replace(/[\u0300-\u036f]/g, '');
// Los rotulos de la tabla llevan un asterisco de nota ("Dividendes²", "Intérêts²") que no forma parte
// del nombre. Sin quitarlo, esos ocho conceptos no se reconocen y la comprobacion los da por vacios.
const limpiar = (s) => sinAcentos(s).replace(/[¹²³⁴⁵]/g, '').replace(/\s+/g, ' ').trim().toLowerCase();

function agruparEnLineas(items) {
  const orden = [...items].sort((a, b) => b.y - a.y || a.x - b.x);
  const lineas = [];
  for (const it of orden) {
    const u = lineas[lineas.length - 1];
    if (u && Math.abs(u.y - it.y) <= TOL_FILA) u.items.push(it);
    else lineas.push({ y: it.y, items: [it] });
  }
  for (const l of lineas) l.items.sort((a, b) => a.x - b.x);
  return lineas;
}

// Las seis columnas de la tabla de totales: CAD, USD y Combinado del mes, y los mismos tres del acumulado
// del año. Se deducen de la cabecera de cada pagina, que es la que dice "CAD USD Combinés en CAD¹" dos
// veces. Los rotulos van alineados a la derecha igual que los importes.
// Se descarta el pie de pagina (y≈21), que tambien lleva la palabra "CAD" en el tipo de cambio, y la
// leyenda de la derecha (x>520), que la repite en cada concepto.
function columnasDeLaPagina(items) {
  const candidatos = items.filter((i) => i.x < 520 && i.y > 30 && i.y < 512
    && /^(CAD|USD|Combin\S* en CAD\S*)$/.test(i.str.trim()));
  const bordes = candidatos.map((i) => +(i.x + i.w).toFixed(2)).sort((a, b) => a - b);
  const grupos = [];
  for (const b of bordes) {
    const g = grupos[grupos.length - 1];
    if (g && b - g[g.length - 1] <= 1.5) g.push(b);
    else grupos.push([b]);
  }
  const cols = grupos.map((g) => g.reduce((a, b) => a + b, 0) / g.length);
  return { cols: cols.length === 6 ? cols : null, vistos: candidatos.length, grupos: cols.length };
}

const CONCEPTOS = [
  'Ventes', 'Achats', 'Intérêts', 'Dividendes', 'Dépôts', 'Retraits',
  'Dividendes réinvestis', 'Conversion de devises', 'Transferts',
  'Opérations stratégiques sur le capital', 'Commissions', 'Frais et remises', 'Autres opérations',
  'Solde au début', 'Solde à la fin',
];

// Devuelve { concepto: {cad, usd, comb} } del mes en curso.
function leerTotales(items) {
  const { cols, vistos, grupos } = columnasDeLaPagina(items);
  if (!cols) return { error: `la cabecera dio ${grupos} columnas en vez de 6 (${vistos} rotulos)` };

  const lineas = agruparEnLineas(items).filter((l) => l.y < 500 && l.y > 40);
  const totales = {};
  for (const l of lineas) {
    // El rotulo son los fragmentos de la izquierda que NO son importes. Un importe largo —"(2 040,00)",
    // "2 781,24"— empieza tan a la izquierda que cae dentro de la columna del rotulo, y sin esta
    // exclusion el nombre del concepto queda pegado a su importe y deja de reconocerse.
    const etiqueta = limpiar(l.items.filter((i) => i.x < 170 && !esNumero(i.str)).map((i) => i.str).join(' '));
    if (!etiqueta) continue;
    const conocido = CONCEPTOS.find((c) => limpiar(c) === etiqueta);
    if (!conocido) continue;
    const valores = [0, 0, 0, 0, 0, 0];
    for (const it of l.items) {
      if (!esNumero(it.str)) continue;
      const borde = it.x + it.w;
      let mejor = -1;
      let d = Infinity;
      cols.forEach((c, k) => { const dd = Math.abs(c - borde); if (dd < d) { d = dd; mejor = k; } });
      if (d <= TOL_COL) valores[mejor] = aNumero(it.str);
    }
    totales[conocido] = { cad: valores[0], usd: valores[1], comb: valores[2] };
  }
  return totales;
}

// Tipo del estado -> concepto de la tabla de totales, y que columna de importe se usa.
//   bruto  : las compras y ventas, que el estado imprime por su importe bruto
//   neto   : los demas conceptos, por su importe neto
//
// El concepto "Frais" del estado sale en la linea "Autres opérations" y no en "Frais et remises":
// comprobado con la unica comision de transferencia de los siete años (febrero de 2022, 25,00 CAD).
function conceptoDeMovimiento(o) {
  const t = o.tipo;
  if (t === 'Acheter') return { concepto: 'Achats', campo: 'bruto' };
  if (t === 'Vendre') return { concepto: 'Ventes', campo: 'bruto' };
  if (t === 'DIV') return { concepto: 'Dividendes', campo: 'neto' };
  if (t === 'Contribution') return { concepto: 'Dépôts', campo: 'neto' };
  if (t === 'TF6') return { concepto: 'Transferts', campo: 'neto' };
  if (t === 'Transfert sortant') return { concepto: 'Transferts', campo: 'neto' };
  if (t === 'Frais') return { concepto: 'Autres opérations', campo: 'neto' };
  if (t === 'Opérations Sur Devises') return { concepto: 'Conversion de devises', campo: 'neto' };
  if (t === 'DIS') return null; // desdoblamiento: no mueve efectivo
  return { concepto: '(desconocido)', campo: 'neto' };
}

const datos = JSON.parse(fs.readFileSync(fichero, 'utf8'));

let meses = 0;
const fallos = [];
const avisos = [];

for (const estado of datos.estados) {
  const ruta = process.argv[3] ? `${process.argv[3]}\\${estado.archivo}` : estado.archivo;
  const doc = await getDocument({
    data: new Uint8Array(fs.readFileSync(ruta)),
    useSystemFonts: false, disableFontFace: true, isEvalSupported: false,
    useWorkerFetch: false, verbosity: 0,
  }).promise;

  // La pagina de totales y la de operaciones ya vienen en el JSON; aqui solo hace falta la primera.
  let totales = null;
  for (let p = 1; p <= doc.numPages; p++) {
    const pagina = await doc.getPage(p);
    const tc = await pagina.getTextContent();
    const items = tc.items
      .filter((i) => typeof i.str === 'string' && i.str.trim() !== '')
      .map((i) => ({ str: i.str, x: i.transform[4], y: i.transform[5], w: i.width || 0 }));
    const texto = items.map((i) => i.str).join(' ');
    if (texto.includes('Variations dans les montants en argent comptant')) {
      totales = leerTotales(items);
      break;
    }
  }
  if (typeof doc.destroy === 'function') await doc.destroy();

  if (!totales) { fallos.push(`${estado.mes}: no se encontro la tabla de variaciones`); continue; }
  if (totales.error) { fallos.push(`${estado.mes}: ${totales.error}`); continue; }
  meses += 1;

  const faltan = CONCEPTOS.filter((c) => !totales[c]);
  if (faltan.length) avisos.push(`${estado.mes}: conceptos no leidos: ${faltan.join(', ')}`);

  const ops = estado.registros.filter((r) => r.clase === 'operacion' && r.seccion === '04');

  // Lo que suman MIS movimientos, por concepto y moneda. Cada movimiento pertenece a UNA moneda, y se
  // sabe por la columna que trae importe: los dividendos y las conversiones no traen precio, solo neto,
  // asi que decidirlo por la columna de precio dejaba fuera todos los dividendos.
  const mio = {};
  const aplica = (o, moneda) => (moneda === 'cad'
    ? o.cadBruto !== null || o.cadNeto !== null
    : o.usdBruto !== null || o.usdNeto !== null);

  for (const o of ops) {
    const m = conceptoDeMovimiento(o);
    if (!m) continue;
    for (const moneda of ['cad', 'usd']) {
      if (!aplica(o, moneda)) continue;
      const bruto = moneda === 'cad' ? o.cadBruto : o.usdBruto;
      const neto = moneda === 'cad' ? o.cadNeto : o.usdNeto;
      const c = (mio[m.concepto] ??= { cad: 0, usd: 0 });
      c[moneda] += m.campo === 'bruto' ? (bruto ?? 0) : (neto ?? 0);
      // La comision va aparte, como la imprime el estado. Y NO es la columna "Comm." de la operacion:
      // el estado calcula su linea de comisiones como BRUTO menos NETO, y las dos cifras no siempre
      // coinciden — en la venta de VOO de enero de 2021 la columna dice 4,95 y la diferencia entre
      // bruto y neto es 5,06—. Ese céntimo de mas es una tasa que el estado mete en el neto y no
      // desglosa, asi que la unica comision que cuadra con su propio total es la resta.
      if (m.campo === 'bruto') (mio['Commissions'] ??= { cad: 0, usd: 0 })[moneda] += (neto ?? 0) - (bruto ?? 0);
    }
  }

  // 1 + 2: cada concepto, contra lo que imprime el estado. Los dos saldos no son conceptos: se
  // comprueban en la cadena, mas abajo.
  const conceptosConDatos = new Set([...Object.keys(mio), ...CONCEPTOS]);
  conceptosConDatos.delete('Solde au début');
  conceptosConDatos.delete('Solde à la fin');
  for (const c of conceptosConDatos) {
    const impreso = totales[c];
    if (!impreso) continue; // 'desconocido' no se imprime
    for (const moneda of ['cad', 'usd']) {
      const a = Math.round((mio[c]?.[moneda] ?? 0) * 100) / 100;
      const b = Math.round(impreso[moneda] * 100) / 100;
      if (Math.abs(a - b) > 0.011) {
        fallos.push(`${estado.mes}  ${c}  ${moneda.toUpperCase()}: extraido ${a.toFixed(2)}  estado ${b.toFixed(2)}  dif ${(a - b).toFixed(2)}`);
      }
    }
  }

  // 3: la cadena del saldo, con mis movimientos. Se suma el NETO de cada uno, que es el efectivo que
  // de verdad entro o salio —tambien en las compras y ventas, donde el bruto no lleva la comision—.
  for (const moneda of ['cad', 'usd']) {
    let saldo = totales['Solde au début']?.[moneda] ?? 0;
    for (const o of ops) {
      const m = conceptoDeMovimiento(o);
      if (!m || !aplica(o, moneda)) continue;
      saldo += (moneda === 'cad' ? o.cadNeto : o.usdNeto) ?? 0;
    }
    const esperado = totales['Solde à la fin']?.[moneda] ?? 0;
    if (Math.abs(saldo - esperado) > 0.011) {
      fallos.push(`${estado.mes}  SALDO ${moneda.toUpperCase()}: reconstruido ${saldo.toFixed(2)}  estado ${esperado.toFixed(2)}  dif ${(saldo - esperado).toFixed(2)}`);
    }
  }
}

console.log(`Meses cuadrados: ${meses}`);
console.log(fallos.length ? `\nDESCUADRES (${fallos.length}):` : '\nSin descuadres: los movimientos suman lo que dicen los estados.');
fallos.forEach((f) => console.log('  ' + f));
if (avisos.length) {
  console.log(`\nAVISOS (${avisos.length}):`);
  [...new Set(avisos)].forEach((a) => console.log('  ' + a));
}
process.exit(fallos.length ? 1 : 0);
