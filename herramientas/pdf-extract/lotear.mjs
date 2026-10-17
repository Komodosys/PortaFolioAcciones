// Reconstruye los lotes de cada posicion a partir del ultimo estado y de las operaciones del periodo.
//
// Uso:  node lotear.mjs <posiciones.csv> <actividades.csv> <csv-salida>
//
// Idea: se parte del estado final (cantidad y coste que declara el ultimo estado) y se DESHACEN las
// operaciones en orden inverso. Lo que queda al final es la parte anterior al periodo, cuyo coste se
// obtiene de forma exacta aunque su fecha no se conozca.
//
// El metodo de coste es el PROMEDIO, que es el que usa el broker (y el que exige Hacienda en Canada
// para propiedades identicas):
//   compra de q a precio p  ->  cantidad + q ;  coste + q*p
//   venta de s             ->  cantidad - s ;  coste * (cantidad - s)/cantidad
// Deshacer es lo simetrico. Esto hace que una venta parcial NO elija un lote concreto: reparte la
// reduccion proporcionalmente y conserva el coste medio, que es justo lo que hace el broker.
//
// Venta pendiente de liquidar: cuando el estado trae "Segregado" menor que "Cantidad", la cantidad del
// estado todavia no refleja una venta ya ejecutada. Se usa la cantidad efectiva (la segregada).

import fs from 'node:fs';
import path from 'node:path';

const [rutaPos, rutaAct, rutaSalida] = process.argv.slice(2);
if (!rutaPos || !rutaAct || !rutaSalida) {
  console.error('Uso: node lotear.mjs <posiciones.csv> <actividades.csv> <csv-salida>');
  process.exit(1);
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
const n = (v) => { const x = Number(String(v ?? '').replace(/,/g, '')); return Number.isFinite(x) ? x : 0; };

const posiciones = leerCsv(rutaPos);
const actividades = leerCsv(rutaAct);

const opsPorClave = new Map();
for (const a of actividades) {
  const clave = `${a.Cuenta}|${a.Simbolo}`;
  if (!opsPorClave.has(clave)) opsPorClave.set(clave, []);
  opsPorClave.get(clave).push(a);
}
for (const lista of opsPorClave.values()) lista.sort((a, b) => a.Fecha.localeCompare(b.Fecha));

const lotes = [];
const notas = [];

for (const p of posiciones) {
  const clave = `${p.Cuenta}|${p.Simbolo}`;
  const ops = opsPorClave.get(clave) ?? [];

  let cantidad = n(p.Cantidad);
  let coste = n(p.CosteTotal);
  const segregado = n(p.Segregado);
  if (segregado > 0 && segregado < cantidad) {
    coste = coste * (segregado / cantidad);
    cantidad = segregado;
    notas.push(`${p.Cuenta} ${p.Simbolo}: se usa la cantidad segregada (${segregado}) porque hay una venta pendiente de liquidar`);
  }

  // 1) Deshacer las operaciones, de la mas reciente a la mas antigua, para aislar la parte anterior
  //    al periodo.
  let cantPre = cantidad;
  let costePre = coste;
  for (let i = ops.length - 1; i >= 0; i--) {
    const o = ops[i];
    const c = n(o.Cantidad);
    const precio = n(o.Precio);
    if (c > 0) {                        // compra
      cantPre -= c;
      costePre -= c * precio;
    } else {                            // venta
      const s = -c;
      const cantidadPrevia = cantPre + s;
      costePre = cantPre === 0 ? 0 : costePre * (cantidadPrevia / cantPre);
      cantPre = cantidadPrevia;
    }
  }

  const red = (x) => Math.round(x * 100) / 100;
  const moneda = p.Moneda;

  // 2) Construir los lotes vigentes hacia delante. Una venta NO elige un lote: reparte la reduccion
  //    proporcionalmente entre todos, que es lo que hace el metodo de coste medio del broker y lo que
  //    conserva el coste por accion que declara el estado.
  const vigentes = [];
  if (Math.abs(cantPre) > 1e-6) {
    vigentes.push({
      cantidad: cantPre, costeUd: costePre / cantPre, fecha: '', exactitud: 'Sin fecha',
      nota: 'compra anterior al periodo: el coste es exacto, la fecha hay que declararla',
    });
  }
  for (const o of ops) {
    const c = n(o.Cantidad);
    if (c > 0) {
      vigentes.push({
        cantidad: c, costeUd: n(o.Precio), fecha: o.Fecha, exactitud: 'Exacta',
        nota: 'operacion del periodo, con fecha del estado',
      });
    } else {
      const s = -c;
      const total = vigentes.reduce((a, l) => a + l.cantidad, 0);
      const factor = total === 0 ? 0 : (total - s) / total;
      for (const l of vigentes) l.cantidad *= factor;
    }
  }

  for (const l of vigentes) {
    lotes.push({
      Cuenta: p.Cuenta, Simbolo: p.Simbolo, Moneda: moneda,
      Cantidad: red(l.cantidad), CosteUnitario: red(l.costeUd),
      CosteTotal: red(l.cantidad * l.costeUd),
      Fecha: l.fecha, ExactitudFecha: l.exactitud, Nota: l.nota,
    });
  }
}

const cab = ['Cuenta', 'Simbolo', 'Moneda', 'Cantidad', 'CosteUnitario', 'CosteTotal', 'Fecha',
  'ExactitudFecha', 'Nota'];
const out = [cab.join(',')];
for (const l of lotes) out.push(cab.map((c) => l[c]).join(','));
fs.mkdirSync(path.dirname(rutaSalida), { recursive: true });
fs.writeFileSync(rutaSalida, out.join('\n'), 'utf8');

const sinFecha = lotes.filter((l) => l.ExactitudFecha === 'Sin fecha');
console.log(`Lotes reconstruidos: ${lotes.length}`);
console.log(`  con fecha EXACTA (operaciones del periodo): ${lotes.length - sinFecha.length}`);
console.log(`  sin fecha (anteriores al periodo)         : ${sinFecha.length}`);
console.log('');
console.log('LOTES SIN FECHA (hay que declarar cuando se compraron):');
for (const l of sinFecha) {
  console.log(`  ${l.Cuenta} ${l.Simbolo.padEnd(7)} ${l.Moneda}  ${String(l.Cantidad).padStart(9)} uds  coste ${String(l.CosteTotal).padStart(11)}  (${l.CosteUnitario}/ud)`);
}
if (notas.length) {
  console.log('');
  console.log('AVISOS:');
  notas.forEach((x) => console.log('  ' + x));
}
console.log('');
console.log('COMPROBACION: suma de lotes por posicion contra el estado');
console.log('  Cuenta    Simbolo  Mon   Cant.lotes  Cant.estado  Coste.lotes  Coste.estado');
for (const p of posiciones) {
  const clave = `${p.Cuenta}|${p.Simbolo}`;
  const propios = lotes.filter((l) => `${l.Cuenta}|${l.Simbolo}` === clave);
  const cl = propios.reduce((s, l) => s + l.Cantidad, 0);
  const kl = propios.reduce((s, l) => s + l.CosteTotal, 0);
  const segr = n(p.Segregado);
  const cantEstado = (segr > 0 && segr < n(p.Cantidad)) ? segr : n(p.Cantidad);
  const costeEstado = (segr > 0 && segr < n(p.Cantidad)) ? n(p.CosteTotal) * segr / n(p.Cantidad) : n(p.CosteTotal);
  const okC = Math.abs(cl - cantEstado) < 0.01 ? 'OK' : 'DIF';
  const okK = Math.abs(kl - costeEstado) < 0.02 ? 'OK' : 'DIF';
  console.log(`  ${p.Cuenta}  ${p.Simbolo.padEnd(7)} ${p.Moneda}  ${String(Math.round(cl * 10000) / 10000).padStart(10)} ${String(cantEstado).padStart(12)} ${okC}  ${String(Math.round(kl * 100) / 100).padStart(11)} ${String(Math.round(costeEstado * 100) / 100).padStart(12)} ${okK}`);
}
console.log('');
console.log(`CSV: ${rutaSalida}`);
