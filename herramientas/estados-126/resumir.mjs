// Pone la extraccion en claro: un resumen por concepto y la lista completa, en el orden en que
// ocurrieron las cosas.
//
// Uso:  node resumir.mjs <fichero-json-estados> <carpeta-salida>
//
// Las dos "patas" de una conversion de divisas (CAD que salen, USD que entran) se juntan en una sola
// linea: en el estado son dos apuntes, pero para quien lo lee son un solo hecho.

import fs from 'node:fs';
import path from 'node:path';

const fichero = process.argv[2];
const carpeta = process.argv[3] ?? '.';
if (!fichero) {
  console.error('Uso: node resumir.mjs <fichero-json-estados> <carpeta-salida>');
  process.exit(1);
}

const datos = JSON.parse(fs.readFileSync(fichero, 'utf8'));

const CONCEPTO = {
  'Acheter': 'Compra',
  'Vendre': 'Venta',
  'DIV': 'Dividendo',
  'Contribution': 'Aportación',
  'TF6': 'Transferencia entrada',
  'Transfert sortant': 'Transferencia salida',
  'Frais': 'Comisión',
  'Opérations Sur Devises': 'Conversión divisa',
  'DIS': 'Desdoblamiento',
};

// Que sera cada concepto al entrar en la aplicacion. Lo que no tiene sitio en el modelo se dice, en vez de
// forzarlo: una conversion de divisas no es ni un ingreso ni un gasto.
//
// Los nombres son los que se usan luego en el cargador, y reflejan las decisiones tomadas al revisar la
// extraccion:
//   - un traspaso NO es una aportacion, asi que tiene su propio tipo (TransferIn / TransferOut)
//   - la comision de traspaso de 25,00 CAD viene EN POSITIVO en el estado, asi que entra como Deposit y no
//     como Commission, que restaria
//   - las conversiones de divisas no se modelan: la aplicacion lleva un solo saldo en la moneda base
const EN_LA_APP = {
  'Acheter': 'compra',
  'Vendre': 'venta',
  'DIV': 'dividendo',
  'Contribution': 'aportacion',
  'TF6': 'traspaso_entrada',
  'Transfert sortant': 'traspaso_salida',
  'Frais': 'aportacion',
  'Opérations Sur Devises': 'no modelado',
  'DIS': 'desdoblamiento',
};

const ops = datos.estados
  .flatMap((e) => e.registros.filter((r) => r.clase === 'operacion' && r.seccion === '04').map((r) => ({ ...r, mes: e.mes })))
  // Por fecha, y dentro del mismo dia en el orden en que el estado lo imprime: un traspaso y su
  // conversion son el mismo dia y ocurrieron en ese orden.
  .sort((a, b) => a.fecha.localeCompare(b.fecha) || a.mes.localeCompare(b.mes) || (a.orden ?? 0) - (b.orden ?? 0));
const cuenta = datos.estados.find((e) => e.cuenta)?.cuenta ?? '';
// El numero de cuenta se imprime ENMASCARADO. Una salida de consola acaba copiada en un documento, y
// por ahi si se escapa: el repositorio se publica, y los estados de cuenta no son publicos.
const cuentaVisible = cuenta ? `···${cuenta.slice(-3)}` : '···';

// --- Juntar las dos patas de cada conversion.
const filas = [];
const conversiones = new Set();
for (const o of ops) {
  if (o.tipo !== 'Opérations Sur Devises') { filas.push(o); continue; }
  const ya = filas.findIndex((f) => f.tipo === 'Opérations Sur Devises' && f.fecha === o.fecha && !conversiones.has(f));
  if (ya >= 0) {
    const f = filas[ya];
    f.cadNeto = f.cadNeto ?? o.cadNeto;
    f.usdNeto = f.usdNeto ?? o.usdNeto;
    conversiones.add(f);
  } else filas.push({ ...o });
}

const n = (v) => (v === null || v === undefined ? '' : v.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 4 }));
const signo = (v) => (v === null || v === undefined ? '' : (v < 0 ? '−' : '+') + Math.abs(v).toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 }));

console.log(`CUENTA ${cuentaVisible} — REER particulier (Questrade)`);
console.log(`Estados leidos: ${datos.estados.length}   Primer movimiento: ${filas[0].fecha}   Ultimo: ${filas[filas.length - 1].fecha}`);
console.log(`Movimientos: ${filas.length}`);

// --- Resumen por concepto.
console.log('\nRESUMEN POR CONCEPTO');
console.log('Concepto                n     CAD (neto)        USD (neto)   Que sera en la app');
const porConcepto = new Map();
for (const o of filas) {
  const c = porConcepto.get(o.tipo) ?? { n: 0, cad: 0, usd: 0 };
  c.n += 1;
  c.cad += o.cadNeto ?? 0;
  c.usd += o.usdNeto ?? 0;
  porConcepto.set(o.tipo, c);
}
for (const [tipo, c] of [...porConcepto].sort((a, b) => b[1].n - a[1].n)) {
  console.log(`${(CONCEPTO[tipo] ?? tipo).padEnd(22)} ${String(c.n).padStart(3)} ${(c.cad ? c.cad.toFixed(2) : '').padStart(15)} ${(c.usd ? c.usd.toFixed(2) : '').padStart(15)}   ${EN_LA_APP[tipo] ?? '?'}`);
}

// --- Lista completa, por año.
for (const anio of [...new Set(filas.map((f) => f.fecha.slice(0, 4)))]) {
  console.log(`\n=== ${anio} ===`);
  console.log('Fecha       Concepto              Simbolo  Cantidad      Precio    Neto');
  for (const o of filas.filter((f) => f.fecha.startsWith(anio))) {
    const esConversion = o.tipo === 'Opérations Sur Devises';
    const cantidad = o.cantidad === null ? '' : String(o.cantidad);
    const precio = o.cadPrecio ?? o.usdPrecio;
    // La moneda del apunte se sabe por la columna que trae el importe: los traspasos, las aportaciones
    // y las conversiones no llevan precio, asi que mirar la columna de precio los dejaba sin moneda.
    const moneda = o.cadNeto !== null ? 'CAD' : 'USD';
    const neto = moneda === 'CAD' ? o.cadNeto : o.usdNeto;
    const efectivo = neto === null ? 'sin efectivo' : `${signo(neto)} ${moneda}`;
    let linea = `${o.fecha}  ${(CONCEPTO[o.tipo] ?? o.tipo).padEnd(21)} ${(o.simbolo ?? '').padEnd(7)} ${cantidad.padStart(9)} ${(precio === null ? '' : String(precio)).padStart(11)}  ${efectivo.padStart(16)}`;
    if (esConversion) linea += `   (${n(o.cadNeto)} CAD -> ${n(o.usdNeto)} USD)`;
    console.log(linea);
  }
}

// --- CSV
fs.mkdirSync(carpeta, { recursive: true });
const esc = (v) => {
  const s = v === null || v === undefined ? '' : String(v);
  return /[",;\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
};
const cab = ['Fecha', 'Fecha liquidacion', 'Concepto', 'ConceptoApp', 'Simbolo', 'Cantidad',
  'CAD precio', 'CAD bruto', 'CAD comision', 'CAD neto', 'USD precio', 'USD bruto', 'USD comision', 'USD neto',
  'Descripcion', 'Estado', 'Pagina'];
const out = [cab.join(',')];
for (const o of filas) {
  out.push([o.fecha, o.fechaLiquidacion, CONCEPTO[o.tipo] ?? o.tipo, EN_LA_APP[o.tipo] ?? '', o.simbolo,
    o.cantidad, o.cadPrecio, o.cadBruto, o.cadComision, o.cadNeto,
    o.usdPrecio, o.usdBruto, o.usdComision, o.usdNeto, o.descripcion, o.mes, o.pagina].map(esc).join(','));
}
fs.writeFileSync(path.join(carpeta, 'movimientos.csv'), out.join('\n'), 'utf8');
console.log(`\nCSV: ${path.join(carpeta, 'movimientos.csv')}`);
