// Comprueba, contra la API, que la carga ha dejado la cuenta como decia el plan.
//
// Uso:  node comprobar-carga.mjs
//
// Se compara lo que la aplicacion CALCULA con lo que el planificador PREDIJO. Los dos numeros salen de sitios
// distintos —uno de la base, el otro de los estados de cuenta—, asi que si coinciden es que la carga esta bien;
// y si no, la diferencia dice en que concepto esta el fallo.

import fs from 'node:fs';

const BASE = process.env.API ?? 'http://localhost:8080';
const leer = (ruta) => fs.readFileSync(ruta, 'utf8').replace(/^\uFEFF/, '');
const r2 = (n) => Math.round(n * 100) / 100;

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
  const datos = texto ? JSON.parse(texto) : null;
  if (res.status >= 400) throw new Error(`${metodo} ${ruta} -> ${res.status}`);
  return datos;
}

const r = await fetch(`${BASE}/api/auth/csrf`);
guardar(r);
csrf = (await r.json()).token;
csrf = (await api('POST', '/api/auth/login', { username: env.ADMIN_USERNAME, password: env.ADMIN_PASSWORD })).csrfToken ?? csrf;

const carteras = await api('GET', '/api/portfolios');
const cartera = (carteras.portfolios ?? carteras).find((c) => c.name?.trim().endsWith('126'));
if (!cartera) throw new Error('No encuentro la cartera');

const efectivo = await api('GET', `/api/portfolios/${cartera.id}/cash`);
const vista = await api('GET', `/api/portfolios/${cartera.id}/view`);

// Lo que predijo el planificador.
const plan = JSON.parse(leer('.tmp/reconstruccion-126/plan.json'));

const esperado = {
  opening: 20150.10,
  deposits: 10326.73,
  transfersIn: 14296.64,
  transfersOut: 1090.98,
  sales: 67046.81,
  dividends: 789.53,
  commissions: 197.87,
};
// El coste de lo comprado lo calcula la aplicacion como lotes vivos + coste de lo vendido.
const compradoEsperado = 62050.52 + 50320.82;
const saldoEsperado = -1050.39;

let fallos = 0;
const comparar = (nombre, obtenido, querido, tolerancia = 0.02) => {
  const igual = Math.abs((obtenido ?? 0) - querido) <= tolerancia;
  if (!igual) fallos += 1;
  console.log(`  ${nombre.padEnd(26)} ${String(r2(obtenido ?? 0)).padStart(12)}  esperado ${String(r2(querido)).padStart(12)}  ${igual ? 'ok' : 'DIFERENCIA ' + r2((obtenido ?? 0) - querido)}`);
};

console.log('EFECTIVO');
for (const [campo, nombre] of [['opening', 'apertura'], ['deposits', 'aportaciones'], ['transfersIn', 'traspasos recibidos'],
  ['transfersOut', 'traspasos enviados'], ['sales', 'ventas'], ['dividends', 'dividendos'], ['commissions', 'comisiones']]) {
  comparar(nombre, efectivo[campo], esperado[campo]);
}
comparar('coste de lo comprado', efectivo.purchases, compradoEsperado);
comparar('SALDO', efectivo.balance, saldoEsperado);
console.log(`  ${'sin convertir'.padEnd(26)} ${String(efectivo.unconverted).padStart(12)}  (0 = todo convertible)`);
console.log(`  ${'se puede dar por bueno'.padEnd(26)} ${String(efectivo.isKnown).padStart(12)}`);

console.log('\nPOSICIONES');
const esperadas = new Map(plan.activos.map((a) => [a.simbolo, a.lotes.reduce((t, l) => t + l.cantidad, 0)]));
const enVista = new Map((vista.positions ?? []).map((p) => [p.symbol, p.quantity]));
const sinValorar = new Map((vista.positionsWithoutPrice ?? vista.unpricedPositions ?? []).map((p) => [p.symbol, p.quantity]));

// Una posicion que se vendio entera queda a cero y la aplicacion la cuenta como CERRADA, no entre las que
// valora. Eso no es que falte: es que no queda nada.
const aCero = [...esperadas].filter(([, cantidad]) => cantidad < 1e-6).map(([s]) => s);

for (const [simbolo, cantidad] of [...esperadas].sort((a, b) => b[1] - a[1])) {
  if (cantidad < 1e-6) {
    console.log(`  ${simbolo.padEnd(7)} ${'—'.padStart(10)}  vendida entera: se espera cerrada`);
    continue;
  }
  const obtenida = enVista.get(simbolo) ?? sinValorar.get(simbolo);
  const igual = obtenida !== undefined && Math.abs(obtenida - cantidad) < 1e-6;
  if (!igual) fallos += 1;
  console.log(`  ${simbolo.padEnd(7)} ${String(Math.round((obtenida ?? 0) * 1e4) / 1e4).padStart(10)}  esperado ${String(Math.round(cantidad * 1e4) / 1e4).padStart(10)}  ${igual ? 'ok' : 'FALTA O DISTINTA'}`);
}
const cerradasEsperadas = aCero.length;
if ((vista.closedPositions ?? 0) !== cerradasEsperadas) {
  fallos += 1;
  console.log(`  cerradas: ${vista.closedPositions}, se esperaban ${cerradasEsperadas}`);
} else {
  console.log(`  cerradas: ${vista.closedPositions} (${aCero.join(', ')} vendidas enteras)  ok`);
}
const sobrantes = [...enVista.keys()].filter((s) => !esperadas.has(s));
console.log(`  posiciones sobrantes: ${sobrantes.length ? sobrantes.join(', ') : 'ninguna'}`);
if (sobrantes.length) fallos += 1;

console.log('\nTOTALES DE LA CARTERA');
console.log(`  valor  ${r2(vista.totalValue)} ${vista.baseCurrency}`);
console.log(`  coste  ${r2(vista.totalCost)}`);
console.log(`  ganancia ${r2(vista.totalGain)} (${r2((vista.totalReturn ?? 0) * 100)} %)`);
console.log(`  completa: ${vista.isComplete}   cerradas: ${vista.closedPositions}   sin precio: ${(vista.positionsWithoutPrice ?? vista.unpricedPositions ?? []).length}`);
console.log(`  desdoblamientos pendientes: ${(vista.pendingSplits ?? []).length}`);

const informe = await api('GET', '/api/reports/performance');
const fila = (informe.rows ?? informe).find((f) => f.portfolioId === cartera.id);
if (fila) {
  console.log('\nINFORME');
  console.log(`  aportado ${r2(fila.contributed)}   retirado ${r2(fila.withdrawn)}   realizado ${r2(fila.realizedGain ?? 0)}`);
  const realizadoEsperado = 12345.79;
  const ok = Math.abs((fila.realizedGain ?? 0) - realizadoEsperado) < 1;
  if (!ok) fallos += 1;
  console.log(`  ganancia realizada esperada ${realizadoEsperado}  ${ok ? 'ok' : 'DIFERENCIA'}`);
}

console.log(fallos === 0 ? '\nTODO CUADRA' : `\n${fallos} DIFERENCIA(S)`);
process.exit(fallos === 0 ? 0 : 1);
