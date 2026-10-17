// Prueba del catálogo de instrumentos y de la búsqueda en la fuente de mercado.
//
// Lo que se comprueba de verdad:
//   - Que la búsqueda de fuera traduce la bolsa a código MIC y no copia el vocabulario de la fuente.
//   - Que dar de alta dos veces el mismo instrumento NO crea dos, porque eso acabaría en dos precios
//     distintos para el mismo valor.
//   - Que el ISIN se valida de verdad, con su dígito de control.
//   - Que el catálogo compartido solo lo modifica el administrador.

import { crearCliente, crearComprobador, claveDelAdmin } from './cliente.mjs';

const c = crearComprobador();
const admin = crearCliente('admin');

c.titulo('1. El administrador entra');
c.comprobar('entra', 200, (await admin.entrar('admin', claveDelAdmin())).estado);

c.titulo('2. Buscar «Apple» en la fuente de mercado');
const busqueda = await admin.pedir('GET', '/api/instruments/lookup?q=Apple');
c.comprobar('responde', 200, busqueda.estado);
c.comprobar('encuentra algo', true, (busqueda.json?.length ?? 0) > 0);

const apple = (busqueda.json ?? []).find((r) => r.symbol === 'AAPL');
c.comprobar('encuentra AAPL', true, apple !== undefined);
c.nota(`AAPL -> bolsa de la fuente: ${apple?.exchange}, MIC: ${apple?.mic}, tipo: ${apple?.assetType}`);

// ESTA es la comprobacion que importa: la fuente dice 'NMS' y el instrumento tiene que guardarse como
// 'XNAS'. Si se copiara el codigo de la fuente, la misma bolsa tendria dos nombres en la base.
c.comprobar('la bolsa se traduce a MIC', 'XNAS', apple?.mic);
c.comprobar('el tipo se traduce al del dominio', 'Equity', apple?.assetType);

c.titulo('3. Buscar un fondo canadiense: el codigo de la fuente no se parece al nombre');
const fondos = await admin.pedir('GET', '/api/instruments/lookup?q=TD%20Balanced');
c.comprobar('responde', 200, fondos.estado);
const fondo = (fondos.json ?? []).find((r) => r.providerKey?.startsWith('0P'));
c.comprobar('encuentra un fondo con clave de la fuente', true, fondo !== undefined);
c.nota(`clave de la fuente: ${fondo?.providerKey} | simbolo visible: ${fondo?.symbol}`);
c.comprobar('quita el sufijo de bolsa del simbolo visible', false, fondo?.symbol?.includes('.'));

c.titulo('4. Una busqueda demasiado corta se rechaza');
c.comprobar('una letra da 400', 400, (await admin.pedir('GET', '/api/instruments/lookup?q=A')).estado);
c.comprobar('sin texto da 400', 400, (await admin.pedir('GET', '/api/instruments/lookup?q=')).estado);

c.titulo('5. Y una busqueda sin resultados NO es un error');
const sinNada = await admin.pedir('GET', '/api/instruments/lookup?q=zzzzqqqxxxyyy');
c.comprobar('responde 200 con lista vacia', 200, sinNada.estado);
c.comprobar('y la lista esta vacia', 0, sinNada.json?.length ?? -1);

c.titulo('6. Dar de alta AAPL en el catalogo');
const alta = await admin.pedir('POST', '/api/instruments', {
  symbol: 'AAPL',
  name: 'Apple Inc.',
  assetType: 'Equity',
  quoteCurrency: 'USD',
  exchange: 'XNAS',
  valuationMode: 'Market',
  pricingFrequency: 'IntradayDelayed',
  isin: 'US0378331005',
  country: 'US',
  providerKey: 'AAPL',
  brokerCode: 'AAPL',
});
c.comprobar('se crea', 201, alta.estado);
const idApple = alta.json?.id;
c.nota(`instrumento: ${idApple}`);

c.titulo('7. Darlo de alta OTRA VEZ no crea un duplicado');
const repetido = await admin.pedir('POST', '/api/instruments', {
  symbol: 'AAPL',
  name: 'Apple',
  assetType: 'Equity',
  quoteCurrency: 'USD',
  exchange: 'XNAS',
  valuationMode: 'Market',
  pricingFrequency: 'IntradayDelayed',
});
c.comprobar('responde 201', 201, repetido.estado);
// Si creara dos instrumentos, el mismo valor tendria dos precios distintos y la cartera sumaria mal.
c.comprobar('devuelve EL MISMO instrumento', idApple, repetido.json?.id);

c.titulo('8. Buscar en el catalogo, que es otra cosa que buscar fuera');
const porNombre = await admin.pedir('GET', '/api/instruments?q=apple');
c.comprobar('responde', 200, porNombre.estado);
// En minusculas a proposito: en PostgreSQL LIKE distingue mayusculas, y «apple» tiene que encontrar
// «Apple Inc.».
c.comprobar('lo encuentra escrito en minusculas', 1, porNombre.json?.length ?? -1);

const porSimbolo = await admin.pedir('GET', '/api/instruments?q=AAPL');
c.comprobar('lo encuentra por simbolo', 1, porSimbolo.json?.length ?? -1);

const porBroker = await admin.pedir('GET', '/api/instruments?q=AAPL');
c.comprobar('y guarda el codigo del broker', 'AAPL', porBroker.json?.[0]?.brokerCode);

c.titulo('9. El ISIN se valida de verdad');
const isinMalo = await admin.pedir('POST', '/api/instruments', {
  symbol: 'MSFT',
  name: 'Microsoft',
  assetType: 'Equity',
  quoteCurrency: 'USD',
  exchange: 'XNAS',
  valuationMode: 'Market',
  pricingFrequency: 'IntradayDelayed',
  isin: 'US5949181044',
});
// El digito de control de US5949181045 es 5; con 4 no cuadra. La longitud y los caracteres son
// correctos, asi que solo el digito de control lo distingue de una errata.
c.comprobar('un ISIN con el digito de control mal da 400', 400, isinMalo.estado);
c.comprobar('y dice por que', 'instrument.invalid_isin', isinMalo.json?.title);

c.titulo('10. Y el catalogo compartido solo lo modifica el administrador');
const titularB = crearCliente('titularB');
const sufijo = Date.now().toString().slice(-6);
await admin.pedir('POST', '/api/admin/users', {
  userName: `instr${sufijo}`,
  displayName: 'Persona de prueba',
  password: 'Portafolio-2026-instrumentos',
  baseCurrency: 'CAD',
  language: 'es',
  isAdmin: false,
});
c.comprobar('la persona entra', 200, (await titularB.entrar(`instr${sufijo}`, 'Portafolio-2026-instrumentos')).estado);

// Anadir SI puede: hace falta para dar de alta la posicion que acaba de comprar.
const altaPropia = await titularB.pedir('POST', '/api/instruments', {
  symbol: 'VEEV',
  name: 'Veeva Systems',
  assetType: 'Equity',
  quoteCurrency: 'USD',
  exchange: 'XNYS',
  valuationMode: 'Market',
  pricingFrequency: 'IntradayDelayed',
});
c.comprobar('puede anadir un instrumento nuevo', 201, altaPropia.estado);

// Modificar NO: cambiarlo afecta a las carteras de todos a la vez.
const modificar = await titularB.pedir('PUT', `/api/instruments/${idApple}`, {
  symbol: 'AAPL',
  name: 'Nombre cambiado por cualquiera',
  assetType: 'Equity',
  quoteCurrency: 'USD',
  exchange: 'XNAS',
  valuationMode: 'Market',
  pricingFrequency: 'IntradayDelayed',
});
c.comprobar('pero NO puede modificar uno existente', 403, modificar.estado);

const sigueIgual = await admin.pedir('GET', `/api/instruments/${idApple}`);
c.comprobar('y el nombre no ha cambiado', 'Apple Inc.', sigueIgual.json?.name);

c.terminar();
