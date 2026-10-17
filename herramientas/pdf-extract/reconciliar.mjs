// Reconcilia las posiciones del ultimo estado con las operaciones del periodo disponible.
//
// Uso:  node reconciliar.mjs <posiciones.csv> <actividades.csv> <carpeta-salida>
//
// Responde a la pregunta que importa para la carga inicial: de cada posicion, cuanto se explica con
// las operaciones que tenemos y cuanto viene de antes del periodo cubierto.
//
//   cantidad_inicial_implicada = cantidad_del_estado - suma_de_operaciones_del_periodo
//
//   0        -> la posicion se construyo DENTRO del periodo: los lotes son completos y exactos
//   > 0      -> esa cantidad viene de ANTES del periodo: hace falta historial anterior
//   < 0      -> hay un descuadre que hay que investigar
//
// Ademas avisa cuando "Segregado" < "Cantidad": significa que hay una venta pendiente de liquidar y
// la cantidad del estado aun no la refleja.

import fs from 'node:fs';
import path from 'node:path';

const [rutaPos, rutaAct, carpetaSalida] = process.argv.slice(2);
if (!rutaPos || !rutaAct || !carpetaSalida) {
  console.error('Uso: node reconciliar.mjs <posiciones.csv> <actividades.csv> <carpeta-salida>');
  process.exit(1);
}

function leerCsv(ruta) {
  const texto = fs.readFileSync(ruta, 'utf8').replace(/^\uFEFF/, '');
  const lineas = texto.split(/\r?\n/).filter((l) => l.trim());
  const cab = partirLinea(lineas[0]);
  return lineas.slice(1).map((l) => {
    const v = partirLinea(l);
    const o = {};
    cab.forEach((c, i) => { o[c] = v[i] ?? ''; });
    return o;
  });
}

function partirLinea(linea) {
  const campos = [];
  let actual = '';
  let entreComillas = false;
  for (let i = 0; i < linea.length; i++) {
    const c = linea[i];
    if (c === '"') {
      if (entreComillas && linea[i + 1] === '"') { actual += '"'; i++; }
      else entreComillas = !entreComillas;
    } else if (c === ',' && !entreComillas) { campos.push(actual); actual = ''; }
    else actual += c;
  }
  campos.push(actual);
  return campos;
}

const n = (v) => { const x = Number(String(v ?? '').replace(/,/g, '')); return Number.isFinite(x) ? x : 0; };

const posiciones = leerCsv(rutaPos);
const actividades = leerCsv(rutaAct);

const porClave = new Map();
for (const a of actividades) {
  const clave = `${a.Cuenta}|${a.Simbolo}`;
  if (!porClave.has(clave)) porClave.set(clave, []);
  porClave.get(clave).push(a);
}

const filas = [];
for (const p of posiciones) {
  const clave = `${p.Cuenta}|${p.Simbolo}`;
  const ops = porClave.get(clave) ?? [];
  const cantidad = n(p.Cantidad);
  const segregado = n(p.Segregado);
  const netoOps = ops.reduce((s, o) => s + n(o.Cantidad), 0);
  const inicial = cantidad - netoOps;

  let veredicto;
  if (inicial === 0) veredicto = 'COMPLETA';
  else if (inicial > 0) veredicto = `FALTAN ${inicial} de antes del periodo`;
  else veredicto = `DESCUADRE de ${-inicial}`;

  const pendiente = (segregado > 0 && segregado < cantidad)
    ? `venta pendiente de liquidar: ${cantidad - segregado}` : '';

  filas.push({
    Cuenta: p.Cuenta, Simbolo: p.Simbolo, Moneda: p.Moneda,
    Cantidad: cantidad, Segregado: segregado,
    Operaciones: ops.length, NetoOperaciones: netoOps,
    CantidadInicialImplicada: inicial, Veredicto: veredicto, Aviso: pendiente,
    CosteUnitario: p.CosteUnitario, FechaEstado: p.FechaEstado,
  });
}

filas.sort((a, b) => (a.Cuenta + a.Simbolo).localeCompare(b.Cuenta + b.Simbolo));

const cab = ['Cuenta', 'Simbolo', 'Moneda', 'Cantidad', 'Segregado', 'Operaciones', 'NetoOperaciones',
  'CantidadInicialImplicada', 'Veredicto', 'Aviso', 'CosteUnitario', 'FechaEstado'];
const out = [cab.join(',')];
for (const f of filas) out.push(cab.map((c) => f[c]).join(','));
fs.mkdirSync(carpetaSalida, { recursive: true });
fs.writeFileSync(path.join(carpetaSalida, 'reconciliacion.csv'), out.join('\n'), 'utf8');

const completas = filas.filter((f) => f.Veredicto === 'COMPLETA');
const parciales = filas.filter((f) => f.Veredicto.startsWith('FALTAN'));
const descuadres = filas.filter((f) => f.Veredicto.startsWith('DESCUADRE'));

console.log(`Posiciones analizadas: ${filas.length}`);
console.log(`  con historia COMPLETA dentro del periodo : ${completas.length}`);
console.log(`  con historia anterior sin cubrir          : ${parciales.length}`);
console.log(`  con descuadre                             : ${descuadres.length}`);
console.log('');
console.log('DETALLE');
console.log('  Cuenta    Simbolo  Mon  Cant  Segr  Ops  Neto   Inicial  Veredicto');
for (const f of filas) {
  console.log(`  ${f.Cuenta}  ${f.Simbolo.padEnd(7)} ${f.Moneda}  ${String(f.Cantidad).padStart(5)} ${String(f.Segregado).padStart(5)}  ${String(f.Operaciones).padStart(3)}  ${String(f.NetoOperaciones).padStart(5)}  ${String(f.CantidadInicialImplicada).padStart(7)}  ${f.Veredicto}${f.Aviso ? '  [' + f.Aviso + ']' : ''}`);
}
if (completas.length) {
  console.log('');
  console.log('POSICIONES CON LOTES COMPLETOS (se pueden cargar con sus fechas reales):');
  for (const f of completas) {
    const ops = porClave.get(`${f.Cuenta}|${f.Simbolo}`) ?? [];
    for (const o of ops) {
      console.log(`  ${f.Cuenta} ${f.Simbolo}  ${o.Fecha}  ${o.Tipo.padEnd(6)} ${String(o.Cantidad).padStart(9)} ${o.Moneda} @ ${o.Precio}`);
    }
  }
}
console.log('');
console.log(`CSV: ${path.join(carpetaSalida, 'reconciliacion.csv')}`);
