// Prueba de la vista de cartera calculada en el servidor.
//
// Lo importante que se comprueba:
//   - Que la conversión a la moneda base es CONSISTENTE: el mismo tipo de cambio se aplica al valor y al
//     coste. Si cada uno usara uno distinto, la ganancia saldría de la nada.
//   - LA INVARIANTE DEL ADR-011: la rentabilidad en la moneda base es IGUAL a la de la moneda del
//     instrumento, porque el tipo de cambio se cancela. Si no coincidieran, la cuenta estaría mal.
//   - Que un total INCOMPLETO se declara como tal, en vez de presentarse como si fuera el total.
//   - Que sin precio no se inventa nada: la posición se queda fuera y se avisa.

import { crearCliente, crearComprobador, claveDelAdmin } from './cliente.mjs';

const c = crearComprobador();
const admin = crearCliente('admin');

const r = (valor, n = 4) => Math.round(valor * 10 ** n) / 10 ** n;

c.titulo('Preparacion');
await admin.entrar('admin', claveDelAdmin());

const cartera = await admin.pedir('POST', '/api/portfolios', { name: `Vista ${Date.now()}`, baseCurrency: 'CAD' });
c.comprobar('cartera creada, con CAD como moneda base', 201, cartera.estado);
const idCartera = cartera.json?.id;

const enDolares = await admin.pedir('POST', '/api/instruments', {
  symbol: `USD${String(Date.now()).slice(-4)}`,
  name: 'Instrumento en dolares',
  assetType: 'Equity',
  quoteCurrency: 'USD',
  exchange: 'XNYS',
  valuationMode: 'Market',
  pricingFrequency: 'IntradayDelayed',
});

const enCanadienses = await admin.pedir('POST', '/api/instruments', {
  symbol: `CAD${String(Date.now()).slice(-4)}`,
  name: 'Instrumento en dolares canadienses',
  assetType: 'Equity',
  quoteCurrency: 'CAD',
  exchange: 'XTSE',
  valuationMode: 'Market',
  pricingFrequency: 'IntradayDelayed',
});

const sinPrecio = await admin.pedir('POST', '/api/instruments', {
  symbol: `SP${String(Date.now()).slice(-4)}`,
  name: 'Instrumento sin precio',
  assetType: 'MutualFund',
  quoteCurrency: 'USD',
  exchange: 'XTSE',
  valuationMode: 'Nav',
  pricingFrequency: 'DailyNav',
});

c.titulo('1. Una posicion en dolares, con precio manual');
// 10 compradas a 100 y 13 a 200: 23 acciones, 3600 de coste, 156.5217 de media.
const posDolares = await admin.pedir('POST', `/api/portfolios/${idCartera}/positions`, {
  instrumentId: enDolares.json.id,
  lots: [
    { quantity: 10, costPerUnit: 100, purchaseDate: '2026-01-15' },
    { quantity: 13, costPerUnit: 200, purchaseDate: '2026-03-20' },
  ],
});
c.comprobar('posicion creada', 201, posDolares.estado);
const idDolares = posDolares.json?.id;

c.comprobar('precio manual de 210.5 USD', 200, (await admin.pedir('PUT', `/api/positions/${idDolares}/manual-price`, {
  amount: 210.5, currency: 'USD', date: '2026-08-31',
})).estado);

c.titulo('2. Una posicion en dolares canadienses (no necesita conversion)');
const posCad = await admin.pedir('POST', `/api/portfolios/${idCartera}/positions`, {
  instrumentId: enCanadienses.json.id,
  lots: [{ quantity: 10, costPerUnit: 50, purchaseDate: '2026-02-01' }],
});
const idCad = posCad.json?.id;
await admin.pedir('PUT', `/api/positions/${idCad}/manual-price`, {
  amount: 60, currency: 'CAD', date: '2026-08-31',
});

c.titulo('3. Y una sin ningun precio');
const posSinPrecio = await admin.pedir('POST', `/api/portfolios/${idCartera}/positions`, {
  instrumentId: sinPrecio.json.id,
  lots: [{ quantity: 5, costPerUnit: 10, purchaseDate: '2026-04-01' }],
});
const idSinPrecio = posSinPrecio.json?.id;

c.titulo('4. La vista');
const vista = await admin.pedir('GET', `/api/portfolios/${idCartera}/view`);
c.comprobar('responde', 200, vista.estado);
c.comprobar('la moneda base es la de la cartera', 'CAD', vista.json?.baseCurrency);
c.comprobar('valora las dos posiciones con precio', 2, vista.json?.positions?.length ?? -1);
c.comprobar('y avisa de la que no tiene', 1, vista.json?.positionsWithoutPrice?.length ?? -1);
c.comprobar('marca que la posicion que falta es la correcta', idSinPrecio, vista.json?.positionsWithoutPrice?.[0]);

c.titulo('5. Un total INCOMPLETO se declara como tal');
// Presentar un total parcial como si fuera el total es dar una cifra falsa. La bandera es lo que permite
// a la pantalla decir «esto es lo que hay, pero falta algo».
c.comprobar('isComplete es falso', false, vista.json?.isComplete);

c.titulo('6. La posicion en dolares canadienses no se convierte');
const cad = (vista.json?.positions ?? []).find((p) => p.id === idCad);
c.comprobar('valor de mercado en CAD', 600, r(cad?.marketValue));
c.comprobar('y el de la moneda base es EL MISMO', 600, r(cad?.baseMarketValue));
c.comprobar('sin ganancia local distinta', 100, r(cad?.localGain));

c.titulo('7. La posicion en dolares SI se convierte, y de forma consistente');
const usd = (vista.json?.positions ?? []).find((p) => p.id === idDolares);
c.comprobar('valor de mercado en USD', 4841.5, r(usd?.marketValue));
c.comprobar('coste en USD', 3600, r(usd?.cost));
c.comprobar('ganancia local en USD', 1241.5, r(usd?.localGain));
c.comprobar('el precio sale del que se puso a mano', 'manual', usd?.priceSource);
c.comprobar('y lleva su fecha', '2026-08-31', usd?.priceDate);

// La conversion tiene que usar EL MISMO tipo de cambio para el valor y para el coste. Si usara uno
// distinto en cada uno, la ganancia en la moneda base saldria de la nada.
const tasaValor = usd?.baseMarketValue / usd?.marketValue;
const tasaCoste = usd?.baseCost / usd?.cost;
c.comprobar('el mismo tipo de cambio para valor y coste', r(tasaValor, 6), r(tasaCoste, 6));
c.nota(`tipo de cambio aplicado: 1 USD = ${r(tasaValor, 6)} CAD`);
c.comprobar('la ganancia en base es la diferencia de los dos convertidos', r(usd?.baseGain, 2), r(r(usd?.baseMarketValue, 2) - r(usd?.baseCost, 2), 2));

c.titulo('8. LA INVARIANTE DEL ADR-011: el tipo de cambio se cancela');
// Es la misma invariante que se demostro con pruebas en la Fase 2. Si la rentabilidad en dolares y la
// de dolares canadienses no coincidieran, significaria que la conversion esta mezclando cosas.
c.comprobar('rentabilidad en USD', r(usd?.localReturn * 100, 4), r(usd?.baseReturn * 100, 4));
c.nota(`rentabilidad: ${r((usd?.baseReturn ?? 0) * 100, 4)} %, igual en las dos monedas`);

c.titulo('9. Los totales de la cartera');
// La suma de lo valorado: 4841.5 USD convertidos + 600 CAD.
const valorEsperado = r(r(usd?.baseMarketValue, 3) + 600, 2);
c.comprobar('el valor total es la suma de las valoradas', valorEsperado, r(vista.json?.totalValue, 2));
c.comprobar('la ganancia total cuadra', r(r(vista.json?.totalValue, 2) - r(vista.json?.totalCost, 2), 2), r(vista.json?.totalGain, 2));

c.titulo('10. Los pesos suman el total');
const sumaPesos = (vista.json?.positions ?? []).reduce((s, p) => s + (p.weight ?? 0), 0);
c.comprobar('los pesos suman 1', 1, r(sumaPesos, 6));
c.nota(`los pesos: ${(vista.json?.positions ?? []).map((p) => `${p.symbol} ${r((p.weight ?? 0) * 100, 2)}%`).join(', ')}`);

c.titulo('11. AISLAMIENTO: otra persona no ve esta vista');
const sufijo = Date.now().toString().slice(-6);
const claveTitularB = 'Portafolio-2026-vista';
await admin.pedir('POST', '/api/admin/users', {
  userName: `vista${sufijo}`,
  displayName: 'Persona de prueba',
  password: claveTitularB,
  baseCurrency: 'CAD',
  language: 'es',
  isAdmin: false,
});
const titularB = crearCliente('titularB');
c.comprobar('entra', 200, (await titularB.entrar(`vista${sufijo}`, claveTitularB)).estado);
c.comprobar('no ve la vista de la cartera ajena', 404, (await titularB.pedir('GET', `/api/portfolios/${idCartera}/view`)).estado);

c.terminar();
