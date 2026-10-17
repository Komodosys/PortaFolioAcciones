// Crea una cartera de demostracion con posiciones que se ven.
//
// Para que la tabla y los totales se puedan mirar de verdad hacen falta precios, y el precio manual es la
// unica via que existe hoy: el proveedor de precios todavia no esta enganchado a la vista. Se ponen a
// mano, que es justo lo que hara falta para los instrumentos que no publican cotizacion.

const BASE = 'http://localhost:4200';
const cookies = new Map();
let testigo = null;

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

// --- Entrar -----------------------------------------------------------------------------------
const csrf = await pedir('GET', '/api/auth/csrf');
testigo = csrf.json.token;

const entrada = await pedir('POST', '/api/auth/login', {
  userName: 'admin',
  password: process.env.ADMIN_PASSWORD,
});
testigo = entrada.json.csrfToken;

// --- La cartera -------------------------------------------------------------------------------
const cartera = await pedir('POST', '/api/portfolios', {
  name: 'Demostración',
  baseCurrency: 'CAD',
});
const id = cartera.json.id;
console.log(`  cartera creada: ${id}`);

// --- Los instrumentos y sus posiciones --------------------------------------------------------
// Cuatro casos a proposito, para que la pantalla enseñe lo que tiene que enseñar:
// una posicion en dolares, una en dolares canadienses, un fondo, y una SIN PRECIO.
const casos = [
  {
    instrumento: { symbol: 'VEEV', name: 'Veeva Systems Inc.', assetType: 'Equity', quoteCurrency: 'USD', exchange: 'XNYS', valuationMode: 'Market', pricingFrequency: 'IntradayDelayed', providerKey: 'VEEV' },
    lotes: [
      { quantity: 10, costPerUnit: 172.0, purchaseDate: '2026-06-05' },
      { quantity: 7, costPerUnit: 168.859, purchaseDate: '2026-06-08' },
      { quantity: 10, costPerUnit: 164.5, purchaseDate: '2026-06-10' },
      { quantity: 7, costPerUnit: 154.49, purchaseDate: '2026-06-17' },
    ],
    venta: 11,
    precio: { amount: 301.63, currency: 'USD', date: '2026-10-13' },
  },
  {
    instrumento: { symbol: 'MGK', name: 'Vanguard Mega Cap Growth ETF', assetType: 'Etf', quoteCurrency: 'USD', exchange: 'ARCX', valuationMode: 'Market', pricingFrequency: 'IntradayDelayed', providerKey: 'MGK' },
    lotes: [{ quantity: 60, costPerUnit: 40.3, purchaseDate: '2026-04-15' }],
    precio: { amount: 94.19, currency: 'USD', date: '2026-10-13' },
  },
  {
    instrumento: { symbol: 'GIB.A', name: 'CGI Inc.', assetType: 'Equity', quoteCurrency: 'CAD', exchange: 'XTSE', valuationMode: 'Market', pricingFrequency: 'IntradayDelayed', providerKey: 'GIB-A.TO' },
    lotes: [{ quantity: 110, costPerUnit: 115.77, purchaseDate: '2026-02-10' }],
    precio: { amount: 103.87, currency: 'CAD', date: '2026-10-13' },
  },
  {
    instrumento: { symbol: 'TDB2624C', name: 'TD Canadian Equity Pool - Serie W', assetType: 'MutualFund', quoteCurrency: 'CAD', exchange: 'XTSE', valuationMode: 'Nav', pricingFrequency: 'DailyNav', providerKey: '0P0001K312.TO', brokerCode: 'TDB2624C' },
    lotes: [{ quantity: 200, costPerUnit: 21.36, purchaseDate: '2026-01-30' }],
    precio: { amount: 22.68, currency: 'CAD', date: '2026-10-09' },
  },
  {
    // SIN PRECIO a proposito: asi se ve el aviso de que los totales son parciales, que es una de las
    // cosas que mas importa que la pantalla diga bien.
    instrumento: { symbol: 'SINPRECIO', name: 'Instrumento sin precio todavia', assetType: 'MutualFund', quoteCurrency: 'CAD', exchange: 'XTSE', valuationMode: 'Nav', pricingFrequency: 'DailyNav' },
    lotes: [{ quantity: 50, costPerUnit: 10, purchaseDate: '2026-03-01' }],
  },
];

for (const caso of casos) {
  const instrumento = await pedir('POST', '/api/instruments', caso.instrumento);

  if (instrumento.estado !== 201) {
    console.log(`  ${caso.instrumento.symbol}: no se pudo crear el instrumento (${instrumento.estado})`);
    continue;
  }

  const posicion = await pedir('POST', `/api/portfolios/${id}/positions`, {
    instrumentId: instrumento.json.id,
    lots: caso.lotes,
  });

  if (posicion.estado !== 201) {
    console.log(`  ${caso.instrumento.symbol}: no se pudo crear la posicion (${posicion.estado})`);
    continue;
  }

  // La venta parcial, para que se vea que el coste medio se conserva.
  if (caso.venta) {
    await pedir('POST', `/api/positions/${posicion.json.id}/sell`, { quantity: caso.venta });
  }

  if (caso.precio) {
    await pedir('PUT', `/api/positions/${posicion.json.id}/manual-price`, caso.precio);
  }

  console.log(`  ${caso.instrumento.symbol}: posicion creada${caso.precio ? ' con precio' : ' SIN precio'}`);
}

console.log();
console.log('  Listo. Abre http://localhost:4200/carteras/' + id);
