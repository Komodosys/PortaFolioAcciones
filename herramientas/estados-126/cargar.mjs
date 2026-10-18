// Carga la reconstruccion de la cuenta en la aplicacion, a traves de su API.
//
// Uso:
//   node cargar.mjs --simulacro        ensena lo que haria y no escribe nada
//   node cargar.mjs --ejecutar         lo hace
//
// Antes de --ejecutar hay que BORRAR las posiciones que haya, porque se rehacen desde las compras. No hay
// endpoint para borrar una posicion (solo cerrarla, que conserva los lotes), asi que ese paso se hace con SQL
// a mano y con copia de seguridad hecha. Va aparte a proposito: es el unico paso que se salta la auditoria.
//
// EL ORDEN IMPORTA, y es lo que hace que esto funcione:
//   - Un desdoblamiento se aplica a TODA la posicion, asi que hay que crear los lotes anteriores, aplicar el
//     split y despues anadir los posteriores. Al reves, el lote comprado despues del split se multiplicaria.
//   - Una venta toma el coste medio ANTES de vender, asi que las compras anteriores tienen que estar puestas.
//   - Dentro del mismo dia se respeta el orden del estado de cuenta, que es el orden del CSV: si una compra y
//     una venta caen el mismo dia, cambiar el orden cambia el coste medio que se lleva la venta.

import fs from 'node:fs';

const BASE = process.env.API ?? 'http://localhost:8080';
const CARPETA = '.tmp/reconstruccion-126';
const MOVIMIENTOS = 'DOCUMENTACION/movimientos.csv';
const TIPOS = `${CARPETA}/tipos-usd-cad.csv`;

const ejecutar = process.argv.includes('--ejecutar');
const leer = (ruta) => fs.readFileSync(ruta, 'utf8').replace(/^\uFEFF/, '');
const num = (v) => (v === '' || v === undefined ? null : Number(v));
const r2 = (n) => Math.round(n * 100) / 100;

// --- Sesion ---------------------------------------------------------------
const env = Object.fromEntries(leer('.env').split(/\r?\n/)
  .filter((l) => l.includes('=') && !l.trim().startsWith('#'))
  .map((l) => { const i = l.indexOf('='); return [l.slice(0, i).trim(), l.slice(i + 1).trim().replace(/^"|"$/g, '')]; }));

const cookies = new Map();
const guardar = (r) => { for (const c of r.headers.getSetCookie?.() ?? []) { const [p] = c.split(';'); const i = p.indexOf('='); cookies.set(p.slice(0, i), p.slice(i + 1)); } };
const galletas = () => [...cookies].map(([k, v]) => `${k}=${v}`).join('; ');
let csrf = '';

async function api(metodo, ruta, cuerpo) {
  const res = await fetch(`${BASE}${ruta}`, {
    method: metodo,
    headers: { 'Content-Type': 'application/json', Cookie: galletas(), 'X-CSRF-TOKEN': csrf },
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
  });
  guardar(res);
  const texto = await res.text();
  let datos = null;
  try { datos = texto ? JSON.parse(texto) : null; } catch { datos = texto; }
  if (res.status >= 400) {
    throw new Error(`${metodo} ${ruta} -> ${res.status}: ${typeof datos === 'string' ? datos : JSON.stringify(datos).slice(0, 300)}`);
  }
  return datos;
}

async function entrar() {
  const r = await fetch(`${BASE}/api/auth/csrf`);
  guardar(r);
  csrf = (await r.json()).token;
  const sesion = await api('POST', '/api/auth/login', { username: env.ADMIN_USERNAME, password: env.ADMIN_PASSWORD });
  csrf = sesion.csrfToken ?? csrf;
}

// --- Datos de partida -----------------------------------------------------
const csv = leer(MOVIMIENTOS).split(/\r?\n/).filter((l) => l.trim());
function partir(linea) {
  const partes = []; let actual = ''; let entre = false;
  for (let i = 0; i < linea.length; i++) {
    const c = linea[i];
    if (c === '"') { if (entre && linea[i + 1] === '"') { actual += '"'; i++; } else entre = !entre; }
    else if (c === ',' && !entre) { partes.push(actual); actual = ''; } else actual += c;
  }
  partes.push(actual); return partes;
}
const cabecera = partir(csv[0]);
const mov = csv.slice(1).map((linea) => {
  const celdas = partir(linea);
  const f = Object.fromEntries(cabecera.map((k, i) => [k, celdas[i] ?? '']));
  return {
    fecha: f.Fecha, concepto: f.Concepto, app: f.ConceptoApp, simbolo: f.Simbolo,
    cantidad: num(f.Cantidad),
    cadBruto: num(f['CAD bruto']), cadNeto: num(f['CAD neto']),
    usdPrecio: num(f['USD precio']), usdBruto: num(f['USD bruto']), usdNeto: num(f['USD neto']),
    descripcion: f.Descripcion,
  };
}); // el orden del fichero ya es fecha + orden del estado; no se reordena

const esLaApertura = (m) => m.fecha === '2020-09-14' && (m.cadNeto ?? 0) === 0 && Math.abs(m.usdNeto ?? 0) === 15300;

// La comision efectiva de una OPERACION. No es la columna "Comm." del estado: es bruto menos neto, que es lo
// que el propio estado suma en su linea de comisiones. Y se miran las dos monedas: una operacion en dolares
// tiene el bruto y el neto en las columnas de USD, y mirar solo las de CAD da cero.
const comisionDe = (m) => r2(
  Math.abs((m.cadBruto ?? 0) - (m.cadNeto ?? 0)) + Math.abs((m.usdBruto ?? 0) - (m.usdNeto ?? 0)),
);

// --- Acciones por simbolo, en el orden del fichero (que es el del estado) --
const acciones = []; // { tipo, simbolo, ... }
const simbolos = [];
for (const m of mov) {
  if (m.app === 'compra') {
    if (!simbolos.includes(m.simbolo)) simbolos.push(m.simbolo);
    acciones.push({ tipo: 'compra', simbolo: m.simbolo, fecha: m.fecha, cantidad: m.cantidad, precio: Math.abs(m.cadBruto ?? m.usdBruto) / m.cantidad, comision: comisionDe(m) });
  } else if (m.app === 'desdoblamiento') {
    acciones.push({ tipo: 'split', simbolo: m.simbolo, fecha: m.fecha, sumaUnidades: m.cantidad });
  } else if (m.app === 'venta') {
    acciones.push({ tipo: 'venta', simbolo: m.simbolo, fecha: m.fecha, cantidad: m.cantidad, precio: m.usdPrecio, comision: comisionDe(m) });
  }
}

// El ratio del split se deduce de la propia posicion en ese momento, igual que hace planificar.mjs.
const enCartera = new Map();
for (const a of acciones) {
  const actual = enCartera.get(a.simbolo) ?? 0;
  if (a.tipo === 'compra') enCartera.set(a.simbolo, actual + a.cantidad);
  else if (a.tipo === 'venta') enCartera.set(a.simbolo, actual - a.cantidad);
  else if (a.tipo === 'split') {
    const despues = actual + a.sumaUnidades;
    a.ratio = despues / actual;
    enCartera.set(a.simbolo, despues);
  }
}

// --- Movimientos de efectivo ---------------------------------------------
const efectivo = [];
for (const m of mov) {
  if (esLaApertura(m)) continue; // ya esta declarada como apertura
  const moneda = m.cadNeto !== null ? 'CAD' : 'USD';
  const importe = Math.abs((moneda === 'CAD' ? m.cadNeto : m.usdNeto) ?? 0);
  if (importe === 0) continue;
  const nota = m.descripcion.slice(0, 200) || m.concepto;
  if (m.app === 'dividendo') efectivo.push({ kind: 'Dividend', importe, moneda, fecha: m.fecha, nota });
  else if (m.app === 'aportacion') efectivo.push({ kind: 'Deposit', importe, moneda, fecha: m.fecha, nota });
  else if (m.app === 'traspaso_entrada') efectivo.push({ kind: 'TransferIn', importe, moneda, fecha: m.fecha, nota });
  else if (m.app === 'traspaso_salida') efectivo.push({ kind: 'TransferOut', importe, moneda, fecha: m.fecha, nota });
}

// Las comisiones van aparte, como las imprime el estado: bruto menos neto de cada operacion. Solo de las
// operaciones: una aportacion tambien tiene importe en el neto, y sin este filtro se contaria como comision.
const comisiones = [];
for (const m of mov) {
  if (m.app !== 'compra' && m.app !== 'venta') continue;
  const c = comisionDe(m);
  if (c <= 0.001) continue;
  const moneda = m.cadBruto !== null ? 'CAD' : 'USD';
  comisiones.push({ kind: 'Commission', importe: c, moneda, fecha: m.fecha, nota: `comisión de la ${m.app} de ${m.simbolo}` });
}

// --- Resumen del plan -----------------------------------------------------
const cuentaTipo = (t) => acciones.filter((a) => a.tipo === t).length;
console.log(`Acciones sobre posiciones: ${acciones.length}`);
console.log(`  compras (lotes):   ${cuentaTipo('compra')}   en ${simbolos.length} símbolos`);
console.log(`  desdoblamientos:   ${cuentaTipo('split')}`);
console.log(`  ventas:            ${cuentaTipo('venta')}`);
console.log(`Movimientos de efectivo: ${efectivo.length + comisiones.length}`);
console.log(`  ${Object.entries(efectivo.reduce((t, e) => ({ ...t, [e.kind]: (t[e.kind] ?? 0) + 1 }), {})).map(([k, v]) => `${k} ${v}`).join(' · ')}`);
console.log(`  Commission ${comisiones.length}`);

if (!ejecutar) {
  console.log('\nSIMULACRO: no se ha escrito nada. Añade --ejecutar para hacerlo.');
  console.log('\nPrimeras acciones:');
  for (const a of acciones.slice(0, 8)) {
    console.log(a.tipo === 'split'
      ? `  ${a.fecha}  split      ${a.simbolo.padEnd(5)} x${Math.round(a.ratio * 1e6) / 1e6}`
      : `  ${a.fecha}  ${a.tipo.padEnd(7)} ${a.simbolo.padEnd(5)} ${String(a.cantidad).padStart(9)} @ ${a.precio}`);
  }
  process.exit(0);
}

// --- Ejecucion ------------------------------------------------------------
await entrar();

const carteras = await api('GET', '/api/portfolios');
const lista = carteras.portfolios ?? carteras.items ?? carteras;
const cartera = lista.find((c) => c.name && c.name.trim().endsWith('126'));
if (!cartera) throw new Error('No encuentro la cartera que termina en 126');
console.log(`\nCartera: ${cartera.id}`);

const idDeInstrumento = new Map();
for (const simbolo of simbolos) {
  const resultado = await api('GET', `/api/instruments?q=${encodeURIComponent(simbolo)}`);
  const encontrados = resultado.instruments ?? resultado.items ?? resultado;
  const exacto = encontrados.find((i) => (i.symbol ?? i.ticker) === simbolo);

  if (exacto) { idDeInstrumento.set(simbolo, exacto.id); continue; }

  // Un valor que se vendió del todo nunca hizo falta en el catálogo, así que hay que darlo de alta. La clase
  // de activo y la bolsa se preguntan a la fuente, que es la que lo sabe; no se adivinan.
  const buscado = await api('GET', `/api/instruments/lookup?q=${encodeURIComponent(simbolo)}`);
  const deLaFuente = (buscado.instruments ?? buscado ?? [])[0];
  if (!deLaFuente) throw new Error(`No encuentro ${simbolo} ni en el catálogo ni en la fuente`);

  const tipoDeActivo = deLaFuente.assetType ?? 'Equity';
  const esFondo = tipoDeActivo === 'Fund' || tipoDeActivo === 'MutualFund';

  const alta = await api('POST', '/api/instruments', {
    symbol: simbolo,
    name: deLaFuente.name,
    assetType: tipoDeActivo,
    quoteCurrency: deLaFuente.currency ?? 'USD',
    exchange: deLaFuente.mic ?? 'XNAS',
    valuationMode: esFondo ? 'Nav' : 'Market',
    pricingFrequency: esFondo ? 'DailyNav' : 'IntradayDelayed',
    isin: null,
    country: null,
    providerKey: deLaFuente.providerKey ?? simbolo,
    brokerCode: null,
  });
  idDeInstrumento.set(simbolo, alta.id);
  console.log(`  dado de alta ${simbolo} (${tipoDeActivo}, ${deLaFuente.mic ?? 'XNAS'})`);
}
console.log(`Instrumentos resueltos: ${idDeInstrumento.size}`);

const posicionDe = new Map();
let hechas = 0;
for (const a of acciones) {
  try {
    if (a.tipo === 'compra') {
      const lote = { quantity: a.cantidad, costPerUnit: a.precio, purchaseDate: a.fecha, dateAccuracy: 'Exact', notes: null };
      if (!posicionDe.has(a.simbolo)) {
        const creada = await api('POST', `/api/portfolios/${cartera.id}/positions`, {
          instrumentId: idDeInstrumento.get(a.simbolo), lots: [lote], notes: null,
        });
        posicionDe.set(a.simbolo, creada.id);
      } else {
        await api('POST', `/api/positions/${posicionDe.get(a.simbolo)}/lots`, lote);
      }
    } else if (a.tipo === 'split') {
      await api('POST', `/api/positions/${posicionDe.get(a.simbolo)}/split`, { ratio: a.ratio, date: a.fecha, apply: true });
    } else {
      await api('POST', `/api/positions/${posicionDe.get(a.simbolo)}/sell`, {
        quantity: a.cantidad, pricePerUnit: a.precio, occurredOn: a.fecha, notes: null,
      });
    }
    hechas += 1;
    if (hechas % 10 === 0) console.log(`  ${hechas}/${acciones.length} acciones sobre posiciones`);
  } catch (error) {
    console.error(`\nFALLO en ${a.tipo} ${a.simbolo} ${a.fecha}: ${error.message}`);
    console.error(`Hechas ${hechas} de ${acciones.length}. La base se puede restaurar del respaldo.`);
    process.exit(1);
  }
}

const todos = [...efectivo, ...comisiones];
let escritos = 0;
for (const m of todos) {
  try {
    await api('POST', `/api/portfolios/${cartera.id}/movements`, {
      kind: m.kind, amount: m.importe, currency: m.moneda, occurredOn: m.fecha, notes: m.nota,
    });
    escritos += 1;
    if (escritos % 20 === 0) console.log(`  ${escritos}/${todos.length} movimientos de efectivo`);
  } catch (error) {
    console.error(`\nFALLO en ${m.kind} ${m.fecha} ${m.importe} ${m.moneda}: ${error.message}`);
    process.exit(1);
  }
}

console.log(`\nListo: ${hechas} acciones sobre posiciones y ${escritos} movimientos de efectivo.`);
console.log('El efectivo calculado por la aplicación y las posiciones se comprueban aparte, en cuadrar-carga.mjs.');
