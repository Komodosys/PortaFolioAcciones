// Los desdoblamientos: deteccion y las DOS salidas.
//
// Se comprueba lo que de verdad importa: que un desdoblamiento sin aplicar se detecta (porque da una
// cifra falsa), que aplicarlo multiplica la cantidad, y que «ya esta reflejado» NO la toca —que es la
// salida que hace falta cuando las cifras se copiaron de un estado de cuenta posterior al
// desdoblamiento, y que sin ella obligaria a elegir entre estropear los datos o no poder quitar el aviso.

import { crearCliente, crearComprobador, claveDelAdmin } from '../prueba-api/cliente.mjs';

const { comprobar, titulo, nota, terminar } = crearComprobador();

const admin = crearCliente('admin');
await admin.entrar('admin', claveDelAdmin());

// ---------------------------------------------------------------------------------------------
titulo('1. El instrumento dice que desdoblamientos conoce');

// Se busca un instrumento que TENGA alguno, sea cual sea: asi la prueba no depende de un dato sembrado a
// mano ni de que la ejecucion anterior no lo haya consumido.
const catalogo = await admin.pedir('GET', '/api/instruments?q=');

const conSplit = (catalogo.json ?? []).find((i) => (i.splits ?? []).length > 0);

comprobar('hay al menos un instrumento con desdoblamiento conocido', true, conSplit !== undefined);

if (!conSplit) {
  terminar();
}

const split = conSplit.splits[0];

comprobar('dice la proporcion', true, split.ratio > 1);
comprobar('y la fecha', true, /^\d{4}-\d{2}-\d{2}$/.test(split.date));

nota(`${conSplit.symbol}: ${split.ratio}:1 el ${split.date}`);

/** Crea una cartera con una posicion en ese instrumento, comprada en la fecha indicada. */
async function posicionCompradaEl(dia) {
  const cartera = await admin.pedir('POST', '/api/portfolios', {
    name: `Desdoblamiento ${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
    baseCurrency: 'CAD',
  });

  const posicion = await admin.pedir('POST', `/api/portfolios/${cartera.json.id}/positions`, {
    instrumentId: conSplit.id,
    lots: [{ quantity: 12, costPerUnit: 100, purchaseDate: dia, dateAccuracy: 'Exact' }],
  });

  return { carteraId: cartera.json.id, posicion: posicion.json };
}

/** El aviso de esa posicion, si lo tiene. */
async function avisoDe(carteraId, posicionId) {
  const vista = await admin.pedir('GET', `/api/portfolios/${carteraId}/view`);

  return (vista.json?.pendingSplits ?? []).find((s) => s.positionId === posicionId);
}

// ---------------------------------------------------------------------------------------------
titulo('2. Aplicarlo multiplica la cantidad y NO cambia el coste');

const antesDeLaFecha = new Date(new Date(`${split.date}T00:00:00Z`).getTime() - 86400000)
  .toISOString()
  .slice(0, 10);

const casoAplicar = await posicionCompradaEl(antesDeLaFecha);

comprobar('la posicion se crea', true, casoAplicar.posicion?.id !== undefined);
comprobar('y recibe el aviso, porque compro antes', true, (await avisoDe(casoAplicar.carteraId, casoAplicar.posicion.id)) !== undefined);

const cantidadAntes = casoAplicar.posicion.quantity;
const costeAntes = casoAplicar.posicion.totalCost;

const aplicado = await admin.pedir('POST', `/api/positions/${casoAplicar.posicion.id}/split`, {
  ratio: split.ratio,
  date: split.date,
  apply: true,
});

comprobar('se aplica', 200, aplicado.estado);

const esperada = Math.round(cantidadAntes * split.ratio * 10000) / 10000;
comprobar('la cantidad se multiplica', esperada, Math.round(aplicado.json?.quantity * 10000) / 10000);

// Un desdoblamiento reparte lo mismo entre mas titulos: el coste total no cambia.
comprobar('y el coste total no cambia', costeAntes, aplicado.json?.totalCost);
comprobar('y deja de avisar', undefined, await avisoDe(casoAplicar.carteraId, casoAplicar.posicion.id));

// ---------------------------------------------------------------------------------------------
titulo('3. «Ya esta reflejado» NO toca la cantidad');

const casoMarcar = await posicionCompradaEl(antesDeLaFecha);

comprobar('recibe el aviso', true, (await avisoDe(casoMarcar.carteraId, casoMarcar.posicion.id)) !== undefined);

const marcado = await admin.pedir('POST', `/api/positions/${casoMarcar.posicion.id}/split`, {
  ratio: split.ratio,
  date: split.date,
  // ESTA es la opcion que hace falta cuando las cifras ya vienen del estado de cuenta posterior.
  apply: false,
});

comprobar('se marca', 200, marcado.estado);

// Lo importante: la cantidad NO cambia. Aplicarlo aqui la multiplicaria por segunda vez y dejaria la
// posicion con cinco veces mas titulos de los que tiene.
comprobar('la cantidad NO cambia', cantidadAntes, marcado.json?.quantity);
comprobar('y el coste tampoco', costeAntes, marcado.json?.totalCost);
comprobar('y tambien deja de avisar', undefined, await avisoDe(casoMarcar.carteraId, casoMarcar.posicion.id));

// ---------------------------------------------------------------------------------------------
titulo('4. Una posicion comprada DESPUES del desdoblamiento no recibe el aviso');

const despuesDeLaFecha = new Date(new Date(`${split.date}T00:00:00Z`).getTime() + (86400000 * 30))
  .toISOString()
  .slice(0, 10);

const casoTarde = await posicionCompradaEl(despuesDeLaFecha);

comprobar('la posicion se crea', true, casoTarde.posicion?.id !== undefined);
comprobar('y NO recibe el aviso, porque compro despues', undefined, await avisoDe(casoTarde.carteraId, casoTarde.posicion.id));

terminar();
