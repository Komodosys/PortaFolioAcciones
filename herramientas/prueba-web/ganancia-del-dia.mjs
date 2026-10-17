// Lo que ha cambiado la cartera HOY.
//
// Lo que se comprueba aqui es lo que hace que la cifra sirva de algo: que se calcule con el cierre
// anterior, que se convierta a la moneda base, y sobre todo que **cuando falta el cierre anterior se diga
// que falta** en vez de contar un cero. Un cero diria «no ha cambiado nada», y lo que pasa es que no se
// sabe: esa diferencia es la que convierte una cifra incompleta en una cifra falsa.

import { crearCliente, crearComprobador, claveDelAdmin } from '../prueba-api/cliente.mjs';

const { comprobar, titulo, nota, terminar } = crearComprobador();

const admin = crearCliente('admin');
await admin.entrar('admin', claveDelAdmin());

/** Busca un instrumento del catalogo por su simbolo. */
async function instrumento(simbolo) {
  const r = await admin.pedir('GET', `/api/instruments?q=${encodeURIComponent(simbolo)}`);

  return (r.json ?? []).find((i) => i.symbol === simbolo);
}

/** Crea una cartera con una posicion, y devuelve la vista. */
async function carteraCon(instrumentoId, lote) {
  const cartera = await admin.pedir('POST', '/api/portfolios', {
    name: `Ganancia del dia ${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
    baseCurrency: 'CAD',
  });

  const posicion = await admin.pedir('POST', `/api/portfolios/${cartera.json.id}/positions`, {
    instrumentId: instrumentoId,
    lots: [lote],
  });

  const vista = await admin.pedir('GET', `/api/portfolios/${cartera.json.id}/view`);

  return { carteraId: cartera.json.id, posicionId: posicion.json?.id, vista: vista.json };
}

// ---------------------------------------------------------------------------------------------
titulo('1. Con cierre anterior, la ganancia del dia se calcula');

// Un instrumento que tenga precio de mercado Y un cierre guardado de la sesion anterior.
const conHistorial = await instrumento('AAPL');

if (!conHistorial) {
  nota('no se encontro el instrumento de prueba: no se puede comprobar');
  terminar();
}

const caso = await carteraCon(conHistorial.id, {
  quantity: 10,
  costPerUnit: 100,
  purchaseDate: '2026-01-15',
  dateAccuracy: 'Exact',
});

const fila = caso.vista?.positions?.[0];

comprobar('la posicion se valora', true, fila !== undefined);
comprobar('dice el cierre anterior', true, typeof fila.previousClose === 'number' && fila.previousClose > 0);
comprobar('y el cambio del dia', true, typeof fila.dailyChange === 'number');
comprobar('y su porcentaje', true, typeof fila.dailyReturn === 'number');

// La cuenta: (precio de hoy − cierre anterior) × unidades.
const esperado = Math.round((fila.priceAmount - fila.previousClose) * 10 * 10000) / 10000;
comprobar('el cambio es la diferencia por las unidades', esperado, Math.round(fila.dailyChange * 10000) / 10000);

nota(`${fila.symbol}: ${fila.previousClose} -> ${fila.priceAmount} = ${fila.dailyChange} por 10 unidades`);

comprobar('el total del dia es el de la unica posicion', Math.round(fila.baseDailyChange * 10000) / 10000, Math.round(caso.vista.totalDailyChange * 10000) / 10000);

// Con una sola posicion y su cierre conocido, el total NO esta a medias.
comprobar('y el total del dia esta completo', true, caso.vista.isDailyComplete);
comprobar('asi que el porcentaje SI se da', true, caso.vista.totalDailyReturn !== null);

// ---------------------------------------------------------------------------------------------
titulo('2. Sin cierre anterior se dice que no se sabe, y NO se cuenta como cero');

// Un instrumento nuevo, con precio puesto a mano HOY y sin ningun cierre guardado: no tiene «ayer».
const sinHistorial = await admin.pedir('POST', '/api/instruments', {
  symbol: `SINDIA${Date.now() % 100000}`,
  name: 'Instrumento sin historial de cierres',
  assetType: 'MutualFund',
  quoteCurrency: 'CAD',
  exchange: 'XTSE',
  valuationMode: 'Manual',
  pricingFrequency: 'Manual',
});

comprobar('el instrumento se crea', 201, sinHistorial.estado);

const sinAyer = await carteraCon(sinHistorial.json.id, {
  quantity: 20,
  costPerUnit: 50,
  purchaseDate: '2026-02-01',
  dateAccuracy: 'Exact',
});

// Se le pone un precio a mano de hoy: hay precio, pero no hay con que compararlo.
await admin.pedir('PUT', `/api/positions/${sinAyer.posicionId}/manual-price`, {
  amount: 60,
  currency: 'CAD',
  date: new Date().toISOString().slice(0, 10),
});

const vistaSinAyer = await admin.pedir('GET', `/api/portfolios/${sinAyer.carteraId}/view`);
const filaSinAyer = vistaSinAyer.json?.positions?.[0];

comprobar('la posicion se valora igual', true, filaSinAyer !== undefined);

// ESTA es la comprobacion que importa. Nulo, no cero: no es que no haya cambiado, es que no se sabe.
comprobar('el cambio del dia es NULO, no cero', null, filaSinAyer.dailyChange ?? null);
comprobar('y el porcentaje tambien', null, filaSinAyer.dailyReturn ?? null);

// Y el total de la cartera se declara a medias, en vez de dar un cero que pareceria una jornada plana.
comprobar('el total del dia se declara parcial', false, vistaSinAyer.json.isDailyComplete);
comprobar('y el porcentaje del total NO se inventa', null, vistaSinAyer.json.totalDailyReturn ?? null);

nota(`total del dia: ${vistaSinAyer.json.totalDailyChange} (parcial: ${!vistaSinAyer.json.isDailyComplete})`);

// ---------------------------------------------------------------------------------------------
titulo('3. La ganancia del dia no se confunde con la ganancia total');

comprobar('la ganancia total sigue siendo otra cifra', true, caso.vista.totalGain !== caso.vista.totalDailyChange);
comprobar('y la del activo tampoco es la del dia', true, fila.localGain !== fila.dailyChange);

terminar();
