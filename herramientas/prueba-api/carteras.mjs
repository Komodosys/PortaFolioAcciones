// Prueba del CRUD de carteras: archivar, desarchivar, moneda base y orden.
//
// Lo importante que se comprueba:
//   - Que la moneda base se puede PONER y QUITAR (volver a heredar), porque una eleccion que no se puede
//     deshacer se queda fija para siempre.
//   - Que archivar esconde la cartera del listado normal pero NO la borra.
//   - Que todo lo demas sigue protegido por el aislamiento.

import { crearCliente, crearComprobador, claveDelAdmin } from './cliente.mjs';

const c = crearComprobador();
const admin = crearCliente('admin');

c.titulo('Preparacion');
await admin.entrar('admin', claveDelAdmin());

const propia = await admin.pedir('POST', '/api/portfolios', { name: `Propia ${Date.now()}`, registrationType: 'RegisteredRetirement', baseCurrency: 'USD' });
c.comprobar('cartera con moneda propia', 201, propia.estado);
c.comprobar('y la moneda es la que se pidio', 'USD', propia.json?.baseCurrency);
c.comprobar('marcada como que NO hereda', false, propia.json?.inheritsBaseCurrency);
const id = propia.json?.id;

const heredada = await admin.pedir('POST', '/api/portfolios', { name: `Heredada ${Date.now()}`, registrationType: 'NotRegistered' });
c.comprobar('cartera sin moneda propia la hereda', 'CAD', heredada.json?.baseCurrency);
c.comprobar('marcada como que SI hereda', true, heredada.json?.inheritsBaseCurrency);

c.titulo('1. Cambiar la moneda base, y poder volver a heredar');
const enUsd = await admin.pedir('PUT', `/api/portfolios/${id}/base-currency`, { currency: 'EUR' });
c.comprobar('se cambia', 200, enUsd.estado);
c.comprobar('a la moneda pedida', 'EUR', enUsd.json?.baseCurrency);
c.comprobar('y ya no hereda', false, enUsd.json?.inheritsBaseCurrency);

const deNuevoHereda = await admin.pedir('PUT', `/api/portfolios/${id}/base-currency`, { currency: null });
c.comprobar('mandar null vuelve a heredar', 200, deNuevoHereda.estado);
c.comprobar('y hereda la del perfil', 'CAD', deNuevoHereda.json?.baseCurrency);
c.comprobar('marcada como heredada', true, deNuevoHereda.json?.inheritsBaseCurrency);

c.titulo('2. Un codigo de moneda MALFORMADO se rechaza');
// El dominio acepta cualquier codigo de TRES LETRAS por diseno (el proveedor cubre 223 divisas y no
// hay lista cerrada): 'XYZ' seria «valido» y lo resolveria la capa de tipos de cambio como faltante.
// Lo que se rechaza es lo que no tiene forma de codigo: dos letras, cuatro, o digitos.
const corto = await admin.pedir('PUT', `/api/portfolios/${id}/base-currency`, { currency: 'XX' });
c.comprobar('dos letras dan 400', 400, corto.estado);
c.comprobar('y dice por que', 'portfolio.invalid_currency', corto.json?.title);

const largo = await admin.pedir('PUT', `/api/portfolios/${id}/base-currency`, { currency: 'USDOLLAR' });
c.comprobar('mas de tres letras dan 400', 400, largo.estado);

const digitos = await admin.pedir('PUT', `/api/portfolios/${id}/base-currency`, { currency: '123' });
c.comprobar('digitos dan 400', 400, digitos.estado);

c.titulo('3. Archivar esconde la cartera, pero no la borra');
const archivada = await admin.pedir('POST', `/api/portfolios/${id}/archive`);
c.comprobar('se archiva', 200, archivada.estado);
c.comprobar('y queda marcada', true, archivada.json?.isArchived);

const listadoNormal = await admin.pedir('GET', '/api/portfolios');
c.comprobar('no aparece en el listado normal', false, (listadoNormal.json ?? []).some((p) => p.id === id));

const listadoConArchivadas = await admin.pedir('GET', '/api/portfolios?includeArchived=true');
c.comprobar('pero si al pedir las archivadas', true, (listadoConArchivadas.json ?? []).some((p) => p.id === id));

const sigueAhi = await admin.pedir('GET', `/api/portfolios/${id}`);
c.comprobar('y sigue existiendo', 200, sigueAhi.estado);

c.titulo('4. Desarchivar');
const desarchivada = await admin.pedir('POST', `/api/portfolios/${id}/unarchive`);
c.comprobar('se desarchiva', 200, desarchivada.estado);
c.comprobar('y deja de estar archivada', false, desarchivada.json?.isArchived);

c.titulo('5. Reordenar');
const reordenada = await admin.pedir('PUT', `/api/portfolios/${id}/order`, { sortOrder: 7 });
c.comprobar('se reordena', 200, reordenada.estado);
c.comprobar('con el orden pedido', 7, reordenada.json?.sortOrder);

c.titulo('6. AISLAMIENTO: otra persona no puede tocar esta cartera');
const sufijo = Date.now().toString().slice(-6);
const claveTitularB = 'Portafolio-2026-carteras';
await admin.pedir('POST', '/api/admin/users', {
  userName: `carteras${sufijo}`,
  displayName: 'Persona de prueba',
  password: claveTitularB,
  baseCurrency: 'CAD',
  language: 'es',
  isAdmin: false,
});
const titularB = crearCliente('titularB');
c.comprobar('entra', 200, (await titularB.entrar(`carteras${sufijo}`, claveTitularB)).estado);
c.comprobar('no puede archivarla', 404, (await titularB.pedir('POST', `/api/portfolios/${id}/archive`)).estado);
c.comprobar('no puede reordenarla', 404, (await titularB.pedir('PUT', `/api/portfolios/${id}/order`, { sortOrder: 9 })).estado);
c.comprobar('no puede cambiarle la moneda', 404, (await titularB.pedir('PUT', `/api/portfolios/${id}/base-currency`, { currency: 'USD' })).estado);

const intacta = await admin.pedir('GET', `/api/portfolios/${id}`);
c.comprobar('y sigue sin cambios', 7, intacta.json?.sortOrder);

c.titulo('7. El tipo de registro es obligatorio y solo admite los tres valores');
const sinTipo = await admin.pedir('POST', '/api/portfolios', { name: `Sin tipo ${Date.now()}` });
c.comprobar('sin tipo da 400', 400, sinTipo.estado);
c.comprobar('y dice por que', 'portfolio.registration_required', sinTipo.json?.title);

// La etiqueta en espanol NO es el valor: el idioma lo pone la pantalla, no la API. Si la API aceptara
// «REER» tendria que saber en que idioma mira cada persona.
const conEtiqueta = await admin.pedir('POST', '/api/portfolios', {
  name: `Con etiqueta ${Date.now()}`,
  registrationType: 'REER',
});
c.comprobar('la etiqueta «REER» no vale como valor', 400, conEtiqueta.estado);

// `Enum.TryParse` aceptaria «99» y lo convertiria en un valor que no existe, que llegaria a la base.
const conNumero = await admin.pedir('POST', '/api/portfolios', {
  name: `Numero ${Date.now()}`,
  registrationType: '99',
});
c.comprobar('un numero fuera de rango da 400', 400, conNumero.estado);

const delCeli = await admin.pedir('POST', '/api/portfolios', {
  name: `CELI ${Date.now()}`,
  registrationType: 'RegisteredSavings',
});
c.comprobar('con un valor bueno se crea', 201, delCeli.estado);
c.comprobar('y devuelve el tipo pedido', 'RegisteredSavings', delCeli.json?.registrationType);
c.comprobar('y viene con cero posiciones', 0, delCeli.json?.positionCount);
const idCeli = delCeli.json?.id;

c.titulo('8. La edicion cambia nombre, tipo y moneda DE UNA VEZ');
const editada = await admin.pedir('PUT', `/api/portfolios/${idCeli}`, {
  name: 'Renombrada',
  registrationType: 'RegisteredRetirement',
  baseCurrency: 'USD',
});
c.comprobar('se edita', 200, editada.estado);
c.comprobar('con el nombre nuevo', 'Renombrada', editada.json?.name);
c.comprobar('con el tipo nuevo', 'RegisteredRetirement', editada.json?.registrationType);
c.comprobar('y con la moneda nueva', 'USD', editada.json?.baseCurrency);

const editarSinTipo = await admin.pedir('PUT', `/api/portfolios/${idCeli}`, { name: 'X' });
c.comprobar('editar sin tipo da 400', 400, editarSinTipo.estado);

const vuelveAHeredar = await admin.pedir('PUT', `/api/portfolios/${idCeli}`, {
  name: 'Renombrada',
  registrationType: 'RegisteredRetirement',
  baseCurrency: null,
});
c.comprobar('la moneda vacia vuelve a heredar la del perfil', true, vuelveAHeredar.json?.inheritsBaseCurrency);

c.titulo('9. Borrar: SOLO si esta vacia');
const conPosiciones = await admin.pedir('POST', '/api/portfolios', {
  name: `Con posiciones ${Date.now()}`,
  registrationType: 'NotRegistered',
  baseCurrency: 'CAD',
});
const idConPosiciones = conPosiciones.json?.id;

const sufijoSimbolo = Date.now().toString().slice(-6);
const instrumento = await admin.pedir('POST', '/api/instruments', {
  symbol: `T${sufijoSimbolo}`,
  name: 'Instrumento de prueba del borrado',
  assetType: 'Equity',
  quoteCurrency: 'CAD',
  exchange: 'XTSE',
  valuationMode: 'Market',
  pricingFrequency: 'IntradayDelayed',
  providerKey: `T${sufijoSimbolo}`,
  brokerCode: `T${sufijoSimbolo}`,
});
c.comprobar('se crea un instrumento para darle una posicion', 201, instrumento.estado);

const alta = await admin.pedir('POST', `/api/portfolios/${idConPosiciones}/positions`, {
  instrumentId: instrumento.json?.id,
  lots: [{ quantity: 3, costPerUnit: 10, purchaseDate: '2026-07-01' }],
});
c.comprobar('se le da una posicion', 201, alta.estado);

const conUna = await admin.pedir('GET', `/api/portfolios/${idConPosiciones}`);
c.comprobar('y el resumen lo dice', 1, conUna.json?.positionCount);

const rechazo = await admin.pedir('DELETE', `/api/portfolios/${idConPosiciones}`);
c.comprobar('borrar con posiciones da 409 y no 400', 409, rechazo.estado);
c.comprobar('y dice por que', 'portfolio.has_positions', rechazo.json?.title);
c.comprobar('y NO se borra', 200, (await admin.pedir('GET', `/api/portfolios/${idConPosiciones}`)).estado);

c.titulo('10. Borrar una vacia si se permite');
c.comprobar('se borra', 204, (await admin.pedir('DELETE', `/api/portfolios/${idCeli}`)).estado);
c.comprobar('y ya no existe', 404, (await admin.pedir('GET', `/api/portfolios/${idCeli}`)).estado);
c.comprobar('borrarla otra vez da 404', 404, (await admin.pedir('DELETE', `/api/portfolios/${idCeli}`)).estado);

c.titulo('11. AISLAMIENTO: la otra persona no puede editar ni borrar');
c.comprobar(
  'no puede editarla',
  404,
  (
    await titularB.pedir('PUT', `/api/portfolios/${idConPosiciones}`, {
      name: 'Robada',
      registrationType: 'NotRegistered',
    })
  ).estado,
);
c.comprobar('ni borrarla', 404, (await titularB.pedir('DELETE', `/api/portfolios/${idConPosiciones}`)).estado);
c.comprobar(
  'y sigue como estaba',
  conPosiciones.json?.name,
  (await admin.pedir('GET', `/api/portfolios/${idConPosiciones}`)).json?.name,
);

c.terminar();
