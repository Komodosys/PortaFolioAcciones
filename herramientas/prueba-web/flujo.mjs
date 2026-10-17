// Comprueba el flujo del navegador A TRAVES DEL PROXY del servidor de desarrollo.
//
// El cliente de pruebas apunta a la API directamente (:8080). Esto apunta al servidor de Angular
// (:4200), que es lo que ve el navegador: si el proxy estuviera mal, la aplicacion no tendria datos y el
// fallo no apareceria en ninguna otra prueba.

const BASE = 'http://localhost:4200';
const cookies = new Map();
let testigo = null;
let fallos = 0;

function comprobar(descripcion, esperado, obtenido) {
  const ok = esperado === obtenido;
  if (!ok) { fallos++; }
  console.log(`  [${ok ? 'OK ' : 'MAL'}] ${descripcion}: esperado ${esperado}, obtenido ${obtenido}`);
}

/** Redondea a cuatro decimales, que es la precision con la que se guardan los importes. */
function redondear(valor) {
  return valor === null || valor === undefined ? valor : Math.round(valor * 10000) / 10000;
}

async function pedir(metodo, ruta, cuerpo) {
  const cabeceras = {};
  if (cookies.size > 0) cabeceras.cookie = [...cookies].map(([k, v]) => `${k}=${v}`).join('; ');
  if (cuerpo !== undefined) cabeceras['content-type'] = 'application/json';
  if (testigo && metodo !== 'GET') cabeceras['x-csrf-token'] = testigo;

  const r = await fetch(`${BASE}${ruta}`, {
    method: metodo,
    headers: cabeceras,
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
  });

  for (const una of (r.headers.getSetCookie?.() ?? [])) {
    const [par] = una.split(';');
    const corte = par.indexOf('=');
    cookies.set(par.slice(0, corte), par.slice(corte + 1));
  }

  const texto = await r.text();
  let json = null;
  try { json = texto ? JSON.parse(texto) : null; } catch { /* sin JSON */ }
  return { estado: r.status, json };
}

console.log('1. Pedir el testigo CSRF por el proxy');
const csrf = await pedir('GET', '/api/auth/csrf');
comprobar('responde', 200, csrf.estado);
testigo = csrf.json?.token;
comprobar('trae testigo', true, typeof testigo === 'string' && testigo.length > 20);

console.log();
console.log('2. Entrar');
const entrada = await pedir('POST', '/api/auth/login', {
  userName: 'admin',
  password: process.env.ADMIN_PASSWORD,
});
comprobar('entra', 200, entrada.estado);
testigo = entrada.json?.csrfToken ?? testigo;

console.log();
console.log('3. El perfil');
const perfil = await pedir('GET', '/api/auth/me');
comprobar('responde', 200, perfil.estado);
comprobar('es administrador', true, perfil.json?.isAdmin === true);
console.log(`     ${perfil.json?.displayName} · moneda ${perfil.json?.baseCurrency} · idioma ${perfil.json?.language}`);

console.log();
console.log('4. Las carteras');
const carteras = await pedir('GET', '/api/portfolios');
comprobar('responde', 200, carteras.estado);
comprobar('es una lista', true, Array.isArray(carteras.json));
console.log(`     ${carteras.json?.length} cartera(s)`);

console.log();
console.log('5. Y una accion que cambia algo, con testigo');
const nueva = await pedir('POST', '/api/portfolios', {
  name: `Prueba del proxy ${Date.now()}`,
  baseCurrency: 'CAD',
});
comprobar('se crea', 201, nueva.estado);
comprobar('con su moneda', 'CAD', nueva.json?.baseCurrency);

console.log();
console.log('6. La vista de la cartera, que es lo que pinta la tabla de posiciones');
const vista = await pedir('GET', `/api/portfolios/${nueva.json?.id}/view`);
comprobar('responde', 200, vista.estado);
comprobar('trae la moneda base', 'CAD', vista.json?.baseCurrency);
comprobar('trae los totales', true, typeof vista.json?.totalValue === 'number');
comprobar('y la lista de posiciones', true, Array.isArray(vista.json?.positions));
comprobar('y si los totales son completos', true, typeof vista.json?.isComplete === 'boolean');
console.log(
  `     valor ${vista.json?.totalValue} · coste ${vista.json?.totalCost} · posiciones ${vista.json?.positions?.length}`,
);

console.log();
console.log('7. Una cartera que no existe da 404, no una pantalla rota');
const inexistente = await pedir('GET', '/api/portfolios/00000000-0000-0000-0000-000000000000/view');
comprobar('responde 404', 404, inexistente.estado);

console.log();
console.log('8. El buscador: el catalogo de la casa');
const enCatalogo = await pedir('GET', '/api/instruments?q=veeva');
comprobar('responde', 200, enCatalogo.estado);
comprobar('es una lista', true, Array.isArray(enCatalogo.json));
console.log(`     ${enCatalogo.json?.length} instrumento(s) que contienen «veeva»`);

console.log();
console.log('9. Y la busqueda en la fuente de mercado');
const fuera = await pedir('GET', '/api/instruments/lookup?q=Apple');
comprobar('responde', 200, fuera.estado);
const apple = (fuera.json ?? []).find((r) => r.symbol === 'AAPL');
comprobar('encuentra AAPL', true, apple !== undefined);
comprobar('con su clave de fuente', 'AAPL', apple?.providerKey);
// La bolsa viene traducida a MIC: la fuente dice 'NMS' y el instrumento se identifica por 'XNAS'.
comprobar('y la bolsa traducida a MIC', 'XNAS', apple?.mic);

console.log();
console.log('10. Alta de un instrumento desde un resultado de la fuente');
const alta = await pedir('POST', '/api/instruments', {
  symbol: apple.symbol,
  name: apple.name,
  assetType: apple.assetType ?? 'Equity',
  quoteCurrency: apple.currency ?? 'USD',
  exchange: apple.mic,
  valuationMode: 'Market',
  pricingFrequency: 'IntradayDelayed',
  providerKey: apple.providerKey,
  brokerCode: 'AAPL',
});
comprobar('se da de alta (o ya estaba)', true, alta.estado === 201 || alta.estado === 200);
comprobar('con codigo de broker', 'AAPL', alta.json?.brokerCode);

console.log();
console.log('11. Y una posicion con dos compras, por el formulario');
const conPosicion = await pedir('POST', `/api/portfolios/${nueva.json?.id}/positions`, {
  instrumentId: alta.json?.id,
  lots: [
    { quantity: 10, costPerUnit: 100, purchaseDate: '2026-01-15', dateAccuracy: 'Exact' },
    { quantity: 13, costPerUnit: 200, purchaseDate: '2026-03-20', dateAccuracy: 'Exact' },
  ],
});
comprobar('se crea', 201, conPosicion.estado);
comprobar('con la cantidad sumada', 23, conPosicion.json?.quantity);
comprobar('y el coste sumado', 3600, conPosicion.json?.totalCost);
console.log(`     ${conPosicion.json?.symbol}: ${conPosicion.json?.quantity} a ${conPosicion.json?.averageCostPerUnit} de media`);

console.log();
console.log('12. Y la vista de esa cartera ya la incluye');
const vistaConPosicion = await pedir('GET', `/api/portfolios/${nueva.json?.id}/view`);
comprobar('responde', 200, vistaConPosicion.estado);

// La posicion NO esta entre las valoradas —no tiene precio— pero SI entre las que se devuelven sin
// valorar. Es lo que permite que la tabla la enseñe con guiones en vez de hacer pensar que no se guardo.
comprobar('no esta entre las valoradas', 0, vistaConPosicion.json?.positions?.length ?? -1);
comprobar('pero SI se devuelve sin valorar', 1, vistaConPosicion.json?.unpricedPositions?.length ?? -1);
comprobar('con su simbolo', 'AAPL', vistaConPosicion.json?.unpricedPositions?.[0]?.symbol);
comprobar('diciendo por que', 'sin-precio', vistaConPosicion.json?.unpricedPositions?.[0]?.reason);
comprobar('y con su coste, que si se sabe', 3600, vistaConPosicion.json?.unpricedPositions?.[0]?.cost);
comprobar('y el total declarado como parcial', false, vistaConPosicion.json?.isComplete);

const idPosicion = conPosicion.json?.id;

console.log();
console.log('13. Anadir otra compra a la posicion que ya existe');
const conMas = await pedir('POST', `/api/positions/${idPosicion}/lots`, {
  quantity: 7,
  costPerUnit: 100,
  purchaseDate: '2026-05-10',
  dateAccuracy: 'Approximate',
});
comprobar('responde', 200, conMas.estado);
comprobar('la cantidad suma', 30, conMas.json?.quantity);
comprobar('el coste suma', 4300, conMas.json?.totalCost);
comprobar('y el coste medio baja', 143.3333, redondear(conMas.json?.averageCostPerUnit));
// Tres: las dos compras con las que nacio la posicion, mas la que se acaba de anadir.
comprobar('con tres compras', 3, conMas.json?.lots?.length);
// La fecha aproximada se guarda y se declara: es lo que permite avisar de que los calculos que dependan
// de ella son aproximados.
comprobar('y avisa de que hay una fecha aproximada', true, conMas.json?.hasApproximateDates);

console.log();
console.log('14. Vender una parte');
const vendida = await pedir('POST', `/api/positions/${idPosicion}/sell`, { quantity: 10 });
comprobar('responde', 200, vendida.estado);
comprobar('quedan 20', 20, vendida.json?.quantity);
// ESTO es lo importante de la venta: el coste medio NO cambia. Es lo que hace el metodo de coste medio
// del broker, y si cambiara, el coste de la posicion dejaria de coincidir con el estado de cuenta.
comprobar('y el coste medio se conserva', 143.3333, redondear(vendida.json?.averageCostPerUnit));
comprobar('con el coste total proporcional', 2866.6667, redondear(vendida.json?.totalCost));

console.log();
console.log('15. Poner un precio a mano: la posicion pasa a valer');
const conPrecio = await pedir('PUT', `/api/positions/${idPosicion}/manual-price`, {
  amount: 200,
  currency: 'USD',
  date: '2026-10-13',
});
comprobar('responde', 200, conPrecio.estado);
comprobar('con el precio guardado', 200, conPrecio.json?.manualPriceAmount);

const vistaValorada = await pedir('GET', `/api/portfolios/${nueva.json?.id}/view`);
comprobar('ahora SI esta entre las valoradas', 1, vistaValorada.json?.positions?.length ?? -1);
comprobar('y ya no esta entre las no valoradas', 0, vistaValorada.json?.unpricedPositions?.length ?? -1);
comprobar('con el total ya completo', true, vistaValorada.json?.isComplete);
console.log(
  `     valor ${vistaValorada.json?.positions?.[0]?.marketValue} USD · peso ${vistaValorada.json?.positions?.[0]?.weight}`,
);

console.log();
console.log('16. Cerrar y reabrir');
const vistaAntes = await pedir('GET', `/api/portfolios/${nueva.json?.id}/view`);
const valorAntes = vistaAntes.json?.totalValue;
console.log(`     antes de cerrar: valor ${valorAntes} con ${vistaAntes.json?.positions?.length} posicion(es)`);

const cerrada = await pedir('POST', `/api/positions/${idPosicion}/close`, {});
comprobar('se cierra', 200, cerrada.estado);

// Cerrar NO borra: las compras se conservan enteras para poder reabrir, asi que la cantidad sigue ahi.
// Lo que cambia es que deja de contar.
comprobar('conserva las compras', 20, cerrada.json?.quantity);
comprobar('y conserva el coste', 2866.6667, redondear(cerrada.json?.totalCost));

const vistaCerrada = await pedir('GET', `/api/portfolios/${nueva.json?.id}/view`);

// ESTA es la comprobacion que importa, y fallaba: una posicion cerrada SEGUIA contando en los totales,
// porque el lector de la vista se traia todas las posiciones sin mirar la marca de cerrada y la
// valoracion solo descarta las de cantidad cero. Cerrar significa dejar de contar.
comprobar('ya no cuenta en el valor', 0, vistaCerrada.json?.totalValue);
comprobar('ni en el coste', 0, vistaCerrada.json?.totalCost);
comprobar('ni aparece en la tabla', 0, vistaCerrada.json?.positions?.length ?? -1);
comprobar('y se cuenta como cerrada', 1, vistaCerrada.json?.closedPositions);
console.log(`     despues de cerrar: valor ${vistaCerrada.json?.totalValue}, cerradas ${vistaCerrada.json?.closedPositions}`);

const reabierta = await pedir('POST', `/api/positions/${idPosicion}/reopen`, {});
comprobar('se reabre', 200, reabierta.estado);
comprobar('con la misma cantidad', 20, reabierta.json?.quantity);
comprobar('y el mismo coste', 2866.6667, redondear(reabierta.json?.totalCost));

const vistaReabierta = await pedir('GET', `/api/portfolios/${nueva.json?.id}/view`);
comprobar('y vuelve a contar en el valor', valorAntes, vistaReabierta.json?.totalValue);
comprobar('ya no hay cerradas', 0, vistaReabierta.json?.closedPositions);

console.log();
console.log('17. Una posicion que no existe da 404');
const posInexistente = await pedir('GET', '/api/positions/00000000-0000-0000-0000-000000000000');
comprobar('responde 404', 404, posInexistente.estado);

console.log();
console.log('18. Las preferencias: moneda base e idioma');
const perfilAntes = await pedir('GET', '/api/profile');
comprobar('responde', 200, perfilAntes.estado);
console.log(`     ahora: ${perfilAntes.json?.baseCurrency} / ${perfilAntes.json?.language}`);

const cambiado = await pedir('PUT', '/api/profile/preferences', {
  baseCurrency: 'USD',
  language: 'fr',
});
comprobar('se guarda', 200, cambiado.estado);
comprobar('con la moneda nueva', 'USD', cambiado.json?.baseCurrency);
comprobar('y el idioma nuevo', 'fr', cambiado.json?.language);

// Y lo importante: que SE QUEDE. Guardar y no persistir es el fallo clasico de esta pantalla, y no se ve
// en la respuesta del guardado, sino en la lectura siguiente.
const perfilDespues = await pedir('GET', '/api/profile');
comprobar('y sigue puesto al volver a leerlo', 'USD', perfilDespues.json?.baseCurrency);
comprobar('tambien el idioma', 'fr', perfilDespues.json?.language);

const meDespues = await pedir('GET', '/api/auth/me');
comprobar('y la sesion lo dice igual', 'USD', meDespues.json?.baseCurrency);

console.log();
console.log('19. Un codigo mal escrito da 400, NO 404');
const malo = await pedir('PUT', '/api/profile/preferences', {
  baseCurrency: 'US',
  language: 'es',
});
// Un codigo mal formado es un error de quien llama. Devolver 404 haria creer que el perfil se ha perdido,
// que es justo lo contrario de lo que pasa.
comprobar('responde 400', 400, malo.estado);

const idiomaMalo = await pedir('PUT', '/api/profile/preferences', {
  baseCurrency: 'CAD',
  language: 'de',
});
comprobar('y el idioma tambien se valida', 400, idiomaMalo.estado);

// Y lo que se intento cambiar no se ha quedado a medias: el perfil sigue como estaba.
const perfilTrasFallo = await pedir('GET', '/api/profile');
comprobar('sin dejar el perfil a medias', 'USD', perfilTrasFallo.json?.baseCurrency);

console.log();
console.log('20. Se deja como estaba');
const restaurado = await pedir('PUT', '/api/profile/preferences', {
  baseCurrency: perfilAntes.json?.baseCurrency,
  language: perfilAntes.json?.language,
});
comprobar('restaurado', 200, restaurado.estado);
console.log(`     de vuelta a: ${restaurado.json?.baseCurrency} / ${restaurado.json?.language}`);

console.log();
console.log(fallos === 0 ? 'RESULTADO: el navegador tendra datos.' : `RESULTADO: ${fallos} FALLO(S).`);
process.exit(fallos === 0 ? 0 : 1);
