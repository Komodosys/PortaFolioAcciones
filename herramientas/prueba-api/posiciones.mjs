// Prueba de las posiciones: alta, compras, ventas, cierre y precio manual.
//
// Se ancla en datos REALES: los cuatro lotes de VEEV del estado de cuenta de Questrade y la venta de 11
// acciones. Si la venta proporcional está bien, el coste por unidad tiene que seguir siendo el mismo
// después de vender — que es lo que declara el broker y lo que hace que la aplicación cuadre con él.

import { crearCliente, crearComprobador, claveDelAdmin } from './cliente.mjs';

const c = crearComprobador();
const admin = crearCliente('admin');

// Redondeo a n decimales, para comparar importes sin que estorben los ultimos digitos.
const r = (valor, n = 4) => Math.round(valor * 10 ** n) / 10 ** n;

c.titulo('Preparacion: entrar, una cartera y el instrumento');
await admin.entrar('admin', claveDelAdmin());

const cartera = await admin.pedir('POST', '/api/portfolios', { name: `Posiciones ${Date.now()}`, baseCurrency: 'CAD' });
c.comprobar('se crea la cartera', 201, cartera.estado);
const idCartera = cartera.json?.id;

const instrumento = await admin.pedir('POST', '/api/instruments', {
  symbol: 'VEEV',
  name: 'Veeva Systems Inc.',
  assetType: 'Equity',
  quoteCurrency: 'USD',
  exchange: 'XNYS',
  valuationMode: 'Market',
  pricingFrequency: 'IntradayDelayed',
  providerKey: 'VEEV',
  brokerCode: 'VEEV',
});
c.comprobar('se crea el instrumento', 201, instrumento.estado);
const idInstrumento = instrumento.json?.id;

c.titulo('1. Alta de la posicion con los CUATRO lotes reales de VEEV');
const alta = await admin.pedir('POST', `/api/portfolios/${idCartera}/positions`, {
  instrumentId: idInstrumento,
  lots: [
    { quantity: 10, costPerUnit: 172.0, purchaseDate: '2026-06-05' },
    { quantity: 7, costPerUnit: 168.859, purchaseDate: '2026-06-08' },
    { quantity: 10, costPerUnit: 164.5, purchaseDate: '2026-06-10' },
    { quantity: 7, costPerUnit: 154.49, purchaseDate: '2026-06-17' },
  ],
});
c.comprobar('se crea la posicion', 201, alta.estado);
const idPosicion = alta.json?.id;

c.comprobar('la cantidad es la suma de los lotes', 34, alta.json?.quantity);
c.comprobar('el coste total es la suma de los lotes', 5628.443, alta.json?.totalCost);
c.comprobar('el coste medio por unidad', 165.5424, r(alta.json?.averageCostPerUnit));
c.comprobar('el coste queda en la moneda del instrumento', 'USD', alta.json?.costCurrency);
c.comprobar('y se guardan los cuatro lotes', 4, alta.json?.lots?.length ?? -1);
c.comprobar('los lotes vienen del mas antiguo al mas reciente', '2026-06-05', alta.json?.lots?.[0]?.purchaseDate);

c.titulo('2. El coste por unidad sobrevive a una venta proporcional');
// ESTA es la comprobacion que importa: si la venta repartiera el coste de cualquier manera, el coste
// por unidad cambiaria y la aplicacion dejaria de cuadrar con el estado de cuenta del broker.
const venta = await admin.pedir('POST', `/api/positions/${idPosicion}/sell`, { quantity: 11 });
c.comprobar('se vende', 200, venta.estado);
c.comprobar('quedan 23 acciones', 23, venta.json?.quantity);
c.comprobar('el coste por unidad NO cambia', 165.5424, r(venta.json?.averageCostPerUnit));
c.comprobar('el coste total baja a lo que corresponde', 3807.4761, r(venta.json?.totalCost));
c.nota(`la suma de los lotes sigue siendo exacta: ${r((venta.json?.lots ?? []).reduce((s, l) => s + l.quantity, 0), 8)}`);

c.titulo('3. No se puede vender mas de lo que se tiene');
const deMas = await admin.pedir('POST', `/api/positions/${idPosicion}/sell`, { quantity: 100 });
c.comprobar('da 400', 400, deMas.estado);
c.comprobar('y dice por que', 'position.insufficient_quantity', deMas.json?.title);

c.titulo('4. Ni vender una cantidad que no tiene sentido');
c.comprobar('vender 0 da 400', 400, (await admin.pedir('POST', `/api/positions/${idPosicion}/sell`, { quantity: 0 })).estado);
c.comprobar('vender -5 da 400', 400, (await admin.pedir('POST', `/api/positions/${idPosicion}/sell`, { quantity: -5 })).estado);

c.titulo('5. Anadir otra compra recalcula la media');
const compra = await admin.pedir('POST', `/api/positions/${idPosicion}/lots`, {
  quantity: 10,
  costPerUnit: 200,
  purchaseDate: '2026-07-01',
});
c.comprobar('se anade', 200, compra.estado);
c.comprobar('ahora hay 33 acciones', 33, compra.json?.quantity);
// (3807.4761 + 2000) / 33
c.comprobar('y el coste medio se recalcula', 175.9841, r(compra.json?.averageCostPerUnit));

c.titulo('6. Precio manual, con su fecha y su moneda');
const precio = await admin.pedir('PUT', `/api/positions/${idPosicion}/manual-price`, {
  amount: 210.5,
  currency: 'USD',
  date: '2026-08-31',
});
c.comprobar('se pone', 200, precio.estado);
c.comprobar('con su importe', 210.5, precio.json?.manualPriceAmount);
c.comprobar('con su moneda', 'USD', precio.json?.manualPriceCurrency);
c.comprobar('y con su fecha', '2026-08-31', precio.json?.manualPriceDate);

c.titulo('7. Un precio de cero NO es un precio');
// Un cero es un dato que falta disfrazado. Si se aceptara, la posicion valdria cero y el rendimiento
// saldria como una perdida total, que es una mentira.
const cero = await admin.pedir('PUT', `/api/positions/${idPosicion}/manual-price`, {
  amount: 0,
  currency: 'USD',
  date: '2026-08-31',
});
c.comprobar('da 400', 400, cero.estado);
c.comprobar('y dice por que', 'position.invalid_price', cero.json?.title);

c.titulo('8. Desdoblamiento 5:1, como el de MGK');
const antes = await admin.pedir('GET', `/api/positions/${idPosicion}`);
const split = await admin.pedir('POST', `/api/positions/${idPosicion}/split`, { ratio: 5 });
c.comprobar('se aplica', 200, split.estado);
c.comprobar('la cantidad se multiplica por 5', r(antes.json.quantity * 5, 8), r(split.json?.quantity, 8));
// Y el coste por unidad se divide: si no, la posicion valdria cinco veces mas de lo que vale.
c.comprobar('y el coste por unidad se divide', r(antes.json.averageCostPerUnit / 5), r(split.json?.averageCostPerUnit));
c.comprobar('el coste total NO cambia', r(antes.json.totalCost), r(split.json?.totalCost));

c.titulo('9. Cerrar y reabrir');
const cerrada = await admin.pedir('POST', `/api/positions/${idPosicion}/close`);
c.comprobar('se cierra', 200, cerrada.estado);
c.comprobar('y queda marcada como cerrada', true, cerrada.json?.isClosed);
c.comprobar('conservando lo que costo', true, cerrada.json?.totalCost > 0);

const abierta = await admin.pedir('POST', `/api/positions/${idPosicion}/reopen`);
c.comprobar('se reabre', 200, abierta.estado);
c.comprobar('y deja de estar cerrada', false, abierta.json?.isClosed);

c.titulo('10. El listado de la cartera');
const listado = await admin.pedir('GET', `/api/portfolios/${idCartera}/positions`);
c.comprobar('responde', 200, listado.estado);
c.comprobar('trae la posicion', 1, listado.json?.length ?? -1);
c.comprobar('y con su simbolo resuelto', 'VEEV', listado.json?.[0]?.symbol);

c.titulo('11. AISLAMIENTO: otra persona no puede ver ni tocar esta posicion');
const sufijo = Date.now().toString().slice(-6);
const claveTitularB = 'Portafolio-2026-posiciones';
const cuenta = await admin.pedir('POST', '/api/admin/users', {
  userName: `pos${sufijo}`,
  displayName: 'Persona de prueba',
  password: claveTitularB,
  baseCurrency: 'CAD',
  language: 'es',
  isAdmin: false,
});
c.comprobar('se crea la cuenta', 201, cuenta.estado);

const titularB = crearCliente('titularB');
c.comprobar('entra', 200, (await titularB.entrar(`pos${sufijo}`, claveTitularB)).estado);

c.comprobar('no ve la posicion', 404, (await titularB.pedir('GET', `/api/positions/${idPosicion}`)).estado);
c.comprobar('no ve la cartera', 404, (await titularB.pedir('GET', `/api/portfolios/${idCartera}`)).estado);
c.comprobar('el listado le viene vacio', 0, (await titularB.pedir('GET', `/api/portfolios/${idCartera}/positions`)).json?.length ?? -1);
c.comprobar('no puede vender', 404, (await titularB.pedir('POST', `/api/positions/${idPosicion}/sell`, { quantity: 1 })).estado);
c.comprobar('no puede cerrarla', 404, (await titularB.pedir('POST', `/api/positions/${idPosicion}/close`)).estado);

const intacta = await admin.pedir('GET', `/api/positions/${idPosicion}`);
c.comprobar('y la posicion sigue abierta y sin tocar', false, intacta.json?.isClosed);

c.terminar();
