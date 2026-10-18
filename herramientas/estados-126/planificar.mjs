// Calcula el plan completo de la reconstruccion y lo ensena, SIN escribir nada en la base.
//
// Uso:  node planificar.mjs <movimientos.csv> <tipos-usd-cad.csv> [posiciones-actuales.json]
//
// Por que hace falta un plan y no basta con «meter los movimientos»:
//
//   Una venta no se puede anotar sobre una posicion que ya no tiene esas acciones. Las posiciones que hay hoy
//   en la base son una FOTO del final: sus lotes son los que sobrevivieron, y las acciones vendidas ya no
//   estan. Anotar la venta de 14 VOO sobre una posicion de 7 no se puede, y anotarla «de cualquier manera»
//   dejaria la cantidad mal.
//
//   Asi que la reconstruccion completa es: rehacer las posiciones desde las 46 compras, aplicar los tres
//   desdoblamientos en su fecha, y vender despues. Es la unica forma de que la cantidad, la ganancia realizada
//   y el efectivo salgan bien a la vez.
//
// Lo que se calcula aqui replica lo que hara la aplicacion:
//   - el coste medio se toma ANTES de vender (PositionRepository.SellAsync)
//   - una venta reparte la reduccion entre todos los lotes y CONSERVA el coste por unidad
//   - el tipo de cambio es el ultimo publicado EN O ANTES de la fecha (TiposDeCambio.DelDiaAsync)
//   - el efectivo suma el BRUTO de la venta y resta la comision como movimiento aparte

import fs from 'node:fs';

// Un fichero escrito desde Windows PowerShell sale con marca de orden de bytes, y `JSON.parse` se atraganta con
// ella. Se quita al leer: es una marca, no contenido.
const leer = (ruta) => fs.readFileSync(ruta, 'utf8').replace(/^\uFEFF/, '');

const [rutaMovimientos, rutaTipos, rutaPosiciones] = process.argv.slice(2);
if (!rutaMovimientos || !rutaTipos) {
  console.error('Uso: node planificar.mjs <movimientos.csv> <tipos-usd-cad.csv> [posiciones-actuales.json]');
  process.exit(1);
}

const r2 = (n) => Math.round(n * 100) / 100;
const r6 = (n) => Math.round(n * 1e6) / 1e6;

// --- Tipos de cambio: el ultimo publicado en o antes de la fecha, como hace la aplicacion.
const tipos = leer(rutaTipos).trim().split(/\r?\n/)
  .map((l) => l.split(';'))
  .filter((p) => p.length === 2)
  .map(([fecha, valor]) => ({ fecha, valor: Number(valor) }))
  .sort((a, b) => a.fecha.localeCompare(b.fecha));

function tipoDel(fecha) {
  let bajo = 0;
  let alto = tipos.length - 1;
  let hallado = null;
  while (bajo <= alto) {
    const medio = (bajo + alto) >> 1;
    if (tipos[medio].fecha <= fecha) { hallado = tipos[medio]; bajo = medio + 1; } else { alto = medio - 1; }
  }
  return hallado ? hallado.valor : null;
}
const tipoHoy = tipos[tipos.length - 1];

// --- Movimientos extraidos.
function partirCsv(linea) {
  const partes = [];
  let actual = '';
  let entreComillas = false;
  for (let i = 0; i < linea.length; i++) {
    const c = linea[i];
    if (c === '"') {
      if (entreComillas && linea[i + 1] === '"') { actual += '"'; i++; } else entreComillas = !entreComillas;
    } else if (c === ',' && !entreComillas) { partes.push(actual); actual = ''; } else actual += c;
  }
  partes.push(actual);
  return partes;
}

const csv = leer(rutaMovimientos).split(/\r?\n/).filter((l) => l.trim());
const cabecera = partirCsv(csv[0]);
const num = (v) => (v === '' || v === undefined ? null : Number(v));

const mov = csv.slice(1).map((linea) => {
  const celdas = partirCsv(linea);
  const f = Object.fromEntries(cabecera.map((k, i) => [k, celdas[i] ?? '']));
  return {
    fecha: f.Fecha,
    concepto: f.Concepto,
    app: f.ConceptoApp,
    simbolo: f.Simbolo,
    cantidad: num(f.Cantidad),
    cadBruto: num(f['CAD bruto']), cadNeto: num(f['CAD neto']),
    usdPrecio: num(f['USD precio']), usdBruto: num(f['USD bruto']), usdNeto: num(f['USD neto']),
    descripcion: f.Descripcion,
  };
}).sort((a, b) => a.fecha.localeCompare(b.fecha));

// La apertura ya esta declarada en la base: 15.300,00 USD el 2020-09-14. Volver a meterla como aportacion
// contaria ese dinero dos veces.
const esLaApertura = (m) => m.fecha === '2020-09-14' && (m.cadNeto ?? 0) === 0 && Math.abs(m.usdNeto ?? 0) === 15300;

// La comision efectiva de una operacion. NO es la columna "Comm." del estado: es bruto menos neto, que es lo
// que el propio estado suma en su linea de comisiones.
const comisionDe = (m) => r2(Math.abs((m.cadBruto ?? 0) - (m.cadNeto ?? 0)) + Math.abs((m.usdBruto ?? 0) - (m.usdNeto ?? 0)));

// --- Reconstruccion: compras, desdoblamientos y ventas, en orden cronologico.
const posiciones = new Map();
const ventas = [];
const comisiones = [];

for (const m of mov) {
  if (m.app === 'compra') {
    const p = posiciones.get(m.simbolo) ?? { lotes: [], realizado: 0 };
    const bruto = m.cadBruto ?? m.usdBruto;
    // El coste del lote es cantidad por precio. La comision va aparte, como movimiento propio, porque es lo que
    // hace que el efectivo cuadre con el del estado, que tambien la imprime aparte.
    p.lotes.push({ cantidad: m.cantidad, costeUnitario: Math.abs(bruto) / m.cantidad, fecha: m.fecha });
    posiciones.set(m.simbolo, p);
    const comision = comisionDe(m);
    if (comision > 0.001) comisiones.push({ fecha: m.fecha, moneda: m.cadBruto !== null ? 'CAD' : 'USD', importe: comision, nota: `comisión de la compra de ${m.simbolo}` });
  } else if (m.app === 'desdoblamiento') {
    const p = posiciones.get(m.simbolo);
    if (!p) { console.log(`AVISO: desdoblamiento de ${m.simbolo} sin posicion`); continue; }
    const antes = p.lotes.reduce((t, l) => t + l.cantidad, 0);
    const despues = antes + m.cantidad;
    const ratio = despues / antes;
    for (const l of p.lotes) { l.cantidad *= ratio; l.costeUnitario /= ratio; }
    console.log(`  desdoblamiento ${m.fecha} ${m.simbolo}: ${r6(antes)} -> ${r6(despues)} (x${r6(ratio)})`);
  } else if (m.app === 'venta') {
    const p = posiciones.get(m.simbolo);
    if (!p) { console.log(`AVISO: venta de ${m.simbolo} sin posicion`); continue; }
    const enCartera = p.lotes.reduce((t, l) => t + l.cantidad, 0);
    const costeTotal = p.lotes.reduce((t, l) => t + l.cantidad * l.costeUnitario, 0);
    const costeMedio = costeTotal / enCartera;
    if (m.cantidad > enCartera + 1e-9) console.log(`  AVISO: venta de ${m.cantidad} ${m.simbolo} el ${m.fecha} con solo ${r6(enCartera)} en cartera`);
    const quedan = enCartera - m.cantidad;
    for (const l of p.lotes) l.cantidad = l.cantidad * (quedan / enCartera);
    const ganancia = m.cantidad * (m.usdPrecio - costeMedio);
    p.realizado += ganancia;
    ventas.push({ fecha: m.fecha, simbolo: m.simbolo, cantidad: m.cantidad, precio: m.usdPrecio, costeMedio, ganancia });
    const comision = comisionDe(m);
    if (comision > 0.001) comisiones.push({ fecha: m.fecha, moneda: m.cadBruto !== null ? 'CAD' : 'USD', importe: comision, nota: `comisión de la venta de ${m.simbolo}` });
  }
}

// --- Posiciones que saldrian.
const activos = [...posiciones].filter(([, p]) => p.lotes.reduce((t, l) => t + l.cantidad, 0) > 1e-9);

console.log('\nPOSICIONES QUE SALDRÍAN (reconstruidas desde las compras)');
console.log('Símbolo   Unidades      Coste/ud        Coste total   Ganancia realizada');
let costeLotesCAD = 0;
for (const [simbolo, p] of activos.sort((a, b) => b[1].lotes.reduce((t, l) => t + l.cantidad, 0) - a[1].lotes.reduce((t, l) => t + l.cantidad, 0))) {
  const unidades = p.lotes.reduce((t, l) => t + l.cantidad, 0);
  const coste = p.lotes.reduce((t, l) => t + l.cantidad * l.costeUnitario, 0);
  costeLotesCAD += p.lotes.reduce((t, l) => t + l.cantidad * l.costeUnitario * (tipoDel(l.fecha) ?? 1), 0);
  console.log(`${simbolo.padEnd(8)} ${r6(unidades).toFixed(4).padStart(10)} ${r2(coste / unidades).toFixed(2).padStart(12)} ${r2(coste).toFixed(2).padStart(16)} ${r2(p.realizado).toFixed(2).padStart(20)}`);
}

if (rutaPosiciones) {
  const actuales = JSON.parse(leer(rutaPosiciones));
  console.log('\nCONTRA LO QUE HAY HOY EN LA BASE');
  for (const a of actuales) {
    const p = posiciones.get(a.simbolo);
    const calc = p ? p.lotes.reduce((t, l) => t + l.cantidad, 0) : 0;
    console.log(`  ${a.simbolo.padEnd(7)} base=${String(a.cantidad).padStart(8)}  reconstruido=${r6(calc).toFixed(4).padStart(10)}  ${Math.abs(calc - a.cantidad) < 1e-6 ? 'IGUAL' : 'DISTINTO'}`);
  }
  for (const [simbolo, p] of activos) {
    if (!actuales.some((a) => a.simbolo === simbolo)) {
      console.log(`  ${simbolo.padEnd(7)} base=(no está)  reconstruido=${r6(p.lotes.reduce((t, l) => t + l.cantidad, 0)).toFixed(4).padStart(10)}  FALTA EN LA BASE`);
    }
  }
}

// --- Efectivo, replicando CashReader.
const enCAD = (importe, fecha) => importe * (fecha ? (tipoDel(fecha) ?? 1) : 1);
const netoEnCAD = (m) => (m.cadNeto ?? 0) + enCAD(m.usdNeto ?? 0, m.fecha);
const suma = (...apps) => mov.filter((m) => apps.includes(m.app)).reduce((t, m) => t + netoEnCAD(m), 0);

const apertureCAD = 15300 * 1.3170; // el tipo con el que ya esta declarada en la base
const laApertura = mov.find(esLaApertura);
const aportaciones = suma('aportacion') - (laApertura ? netoEnCAD(laApertura) : 0);
const traspasosEntrada = suma('traspaso_entrada');
// El neto de un traspaso enviado ya viene en negativo en el CSV; aqui se pone en positivo porque en la formula
// se resta, igual que hace CashReader con el importe, que se guarda siempre positivo.
const traspasosSalida = -suma('traspaso_salida');
const dividendos = suma('dividendo');
const comisionesCAD = comisiones.reduce((t, c) => t + enCAD(c.importe, c.fecha), 0);
const ventasBrutasCAD = ventas.reduce((t, v) => t + enCAD(v.cantidad * v.precio, v.fecha), 0);
const costeVendidoCAD = ventas.reduce((t, v) => t + enCAD(v.cantidad * v.costeMedio, v.fecha), 0);

const saldo = apertureCAD + aportaciones + traspasosEntrada - traspasosSalida
  + ventasBrutasCAD + dividendos - comisionesCAD - costeLotesCAD - costeVendidoCAD;

console.log('\nEFECTIVO QUE SALDRÍA (fórmula de CashReader, en CAD)');
const linea = (etiqueta, valor) => console.log(`  ${etiqueta.padEnd(30)} ${valor.toFixed(2).padStart(13)}`);
linea('apertura ya declarada', apertureCAD);
linea('+ aportaciones', aportaciones);
linea('+ traspasos recibidos', traspasosEntrada);
linea('− traspasos enviados', traspasosSalida);
linea('+ ventas (bruto)', ventasBrutasCAD);
linea('+ dividendos', dividendos);
linea('− comisiones', comisionesCAD);
linea('− coste de los lotes vivos', costeLotesCAD);
linea('− coste de lo vendido', costeVendidoCAD);
console.log(`  ${'='.padEnd(30)} ${'='.padStart(13)}`);
linea('= EFECTIVO', saldo);

const realCAD = enCAD(25.71, tipoHoy.fecha);
console.log(`\n  el estado de septiembre de 2026 dice 0,00 CAD + 25,71 USD = ${realCAD.toFixed(2)} CAD al cambio de hoy`);
console.log(`  diferencia: ${(saldo - realCAD).toFixed(2)} CAD`);

console.log('\nQUÉ SE ESCRIBIRÍA');
const cuenta = (app) => mov.filter((m) => m.app === app).length;
console.log(`  posiciones a rehacer: ${activos.length}${rutaPosiciones ? ` (borrando antes las ${JSON.parse(leer(rutaPosiciones)).length} que hay)` : ''}`);
console.log(`  compras (lotes):      ${cuenta('compra')}`);
console.log(`  ventas:               ${ventas.length}`);
console.log(`  desdoblamientos:      ${cuenta('desdoblamiento')} (aplicados, no marcados)`);
console.log(`  dividendos:           ${cuenta('dividendo')}`);
console.log(`  aportaciones:         ${cuenta('aportacion') - 1} (la del 2020-09-14 ya es la apertura)`);
console.log(`  traspasos recibidos:  ${cuenta('traspaso_entrada')}`);
console.log(`  traspasos enviados:   ${cuenta('traspaso_salida')}`);
console.log(`  comisiones:           ${comisiones.length} (una por operación con gastos)`);
console.log(`  conversiones:         0 (no se modelan, por decisión)`);
console.log(`  ganancia realizada:   ${r2(ventas.reduce((t, v) => t + v.ganancia, 0)).toFixed(2)} USD`);

fs.writeFileSync('.tmp/reconstruccion-126/plan.json', JSON.stringify({ ventas, comisiones, activos: [...posiciones].map(([s, p]) => ({ simbolo: s, lotes: p.lotes, realizado: p.realizado })), saldo }, null, 1), 'utf8');
console.log('\nplan detallado: .tmp/reconstruccion-126/plan.json');
