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

const propia = await admin.pedir('POST', '/api/portfolios', { name: `Propia ${Date.now()}`, baseCurrency: 'USD' });
c.comprobar('cartera con moneda propia', 201, propia.estado);
c.comprobar('y la moneda es la que se pidio', 'USD', propia.json?.baseCurrency);
c.comprobar('marcada como que NO hereda', false, propia.json?.inheritsBaseCurrency);
const id = propia.json?.id;

const heredada = await admin.pedir('POST', '/api/portfolios', { name: `Heredada ${Date.now()}` });
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

c.terminar();
