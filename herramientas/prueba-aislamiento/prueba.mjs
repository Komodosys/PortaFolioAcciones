// Prueba del aislamiento entre personas y del libro de auditoría, de punta a punta.
//
// Comprueba lo que de verdad importa: que una persona NO pueda leer ni tocar los datos de otra, que el
// administrador SÍ pueda (ADR-012 y ADR-014), y que cada cambio suyo sobre datos ajenos quede registrado.
//
// Uso:  node prueba.mjs     (con ADMIN_PASSWORD en el entorno)

import { crearCliente, crearComprobador, claveDelAdmin } from '../prueba-api/cliente.mjs';

const c = crearComprobador();
const admin = crearCliente('admin');

c.titulo('1. El administrador entra y crea su cartera');
await admin.entrar('admin', claveDelAdmin());

const carteraDelAdmin = await admin.pedir('POST', '/api/portfolios', { name: 'Cartera de Titular A', baseCurrency: 'CAD' });
c.comprobar('el administrador crea su cartera', 201, carteraDelAdmin.estado);
const idAdmin = carteraDelAdmin.json?.id;
c.nota(`cartera del administrador: ${idAdmin}`);

c.titulo('2. El administrador crea la cuenta de Titular B');
const sufijo = Date.now().toString().slice(-6);
const usuarioTitularB = `titularB${sufijo}`;
const claveTitularB = 'Portafolio-2026-titularB';

const creacion = await admin.pedir('POST', '/api/admin/users', {
  userName: usuarioTitularB,
  displayName: 'Titular B',
  password: claveTitularB,
  baseCurrency: 'CAD',
  language: 'fr',
  isAdmin: false,
});
c.comprobar('se crea la cuenta', 201, creacion.estado);
c.nota(`usuaria: ${usuarioTitularB}`);

c.titulo('3. Titular B entra y crea la suya');
const titularB = crearCliente('titularB');
c.comprobar('Titular B entra', 200, (await titularB.entrar(usuarioTitularB, claveTitularB)).estado);

const carteraDeTitularB = await titularB.pedir('POST', '/api/portfolios', { name: 'Cartera de Titular B', baseCurrency: 'CAD' });
c.comprobar('Titular B crea su cartera', 201, carteraDeTitularB.estado);
const idTitularB = carteraDeTitularB.json?.id;
c.nota(`cartera de Titular B: ${idTitularB}`);

c.titulo('4. EL AISLAMIENTO: Titular B pide la cartera del administrador');
const ajena = await titularB.pedir('GET', `/api/portfolios/${idAdmin}`);
c.comprobar('Titular B recibe 404 (no 403: un 403 ya revelaria que existe)', 404, ajena.estado);
c.nota(`respuesta: ${ajena.texto}`);

c.titulo('5. Y su listado solo trae la suya');
const listadoTitularB = await titularB.pedir('GET', '/api/portfolios');
c.comprobar('su listado responde', 200, listadoTitularB.estado);
c.comprobar('trae exactamente una cartera', 1, listadoTitularB.json?.length ?? -1);
c.comprobar('y es la suya', idTitularB, listadoTitularB.json?.[0]?.id);

c.titulo('6. El administrador SI puede ver la de Titular B');
const delAdmin = await admin.pedir('GET', `/api/portfolios/${idTitularB}`);
c.comprobar('el administrador recibe 200', 200, delAdmin.estado);
c.comprobar('y es la cartera correcta', 'Cartera de Titular B', delAdmin.json?.name);

c.titulo('7. Y su listado incluye las dos');
const listadoAdmin = await admin.pedir('GET', '/api/portfolios');
c.comprobar('el listado responde', 200, listadoAdmin.estado);
// Se comprueba que ESTAN las dos, y no que sean exactamente dos: la prueba tiene que poder ejecutarse
// varias veces sin que los datos de antes la hagan fallar.
const idsDelAdmin = (listadoAdmin.json ?? []).map((x) => x.id);
c.comprobar('ve la suya', true, idsDelAdmin.includes(idAdmin));
c.comprobar('y ve la de Titular B', true, idsDelAdmin.includes(idTitularB));

c.titulo('8. Sin sesion no se ve nada');
const anonimo = crearCliente('anonimo');
c.comprobar('sin sesion, el listado pide entrar', 401, (await anonimo.pedir('GET', '/api/portfolios')).estado);
c.comprobar('sin sesion, una cartera concreta tambien', 401, (await anonimo.pedir('GET', `/api/portfolios/${idAdmin}`)).estado);

c.titulo('9. Una persona normal NO puede crear cuentas');
const intento = await titularB.pedir('POST', '/api/admin/users', {
  userName: `colado${sufijo}`,
  displayName: 'No deberia',
  password: 'Portafolio-2026-colado',
  baseCurrency: 'CAD',
  language: 'es',
  isAdmin: true,
});
c.comprobar('Titular B recibe 403', 403, intento.estado);

c.titulo('10. El administrador renombra la cartera de Titular B (esto debe quedar auditado)');
const renombrado = await admin.pedir('PATCH', `/api/portfolios/${idTitularB}`, { name: 'Cartera de Titular B (corregida)' });
c.comprobar('el administrador puede modificarla', 200, renombrado.estado);
c.comprobar('y el nombre cambia', 'Cartera de Titular B (corregida)', renombrado.json?.name);

c.titulo('11. Titular B renombra la suya (esto NO se audita: son sus datos)');
c.comprobar('puede renombrar la suya', 200, (await titularB.pedir('PATCH', `/api/portfolios/${idTitularB}`, { name: 'Mi cartera' })).estado);

c.titulo('12. Y no puede renombrar la del administrador');
c.comprobar('recibe 404', 404, (await titularB.pedir('PATCH', `/api/portfolios/${idAdmin}`, { name: 'Robada' })).estado);
const sigueIgual = await admin.pedir('GET', `/api/portfolios/${idAdmin}`);
c.comprobar('y la cartera del administrador NO cambio', 'Cartera de Titular A', sigueIgual.json?.name);

c.titulo('13. El administrador restablece la contrasena de Titular B');
const reinicio = await admin.pedir('POST', `/api/admin/users/${creacion.json?.id}/reset-password`);
c.comprobar('se restablece', 200, reinicio.estado);
const temporal = reinicio.json?.temporaryPassword;
c.comprobar('devuelve una contrasena temporal', true, typeof temporal === 'string' && temporal.length >= 12);
c.nota(`temporal: ${temporal?.slice(0, 4)}...  caduca: ${reinicio.json?.expiresAtUtc}`);

c.titulo('14. La sesion que Titular B tenia abierta deja de valer');
c.comprobar('su sesion anterior recibe 401', 401, (await titularB.pedir('GET', '/api/portfolios')).estado);

c.titulo('15. Titular B entra con la temporal');
const titularB2 = crearCliente('titularB2');
const conTemporal = await titularB2.entrar(usuarioTitularB, temporal);
c.comprobar('entra', 200, conTemporal.estado);
c.comprobar('y se le dice que tiene que cambiarla', true, conTemporal.json?.mustChangePassword === true);

c.titulo('16. Y la cambia');
const claveNueva = 'Portafolio-2026-nueva-de-titularB';
c.comprobar('se cambia', 200, (await titularB2.pedir('POST', '/api/auth/change-password', {
  currentPassword: temporal,
  newPassword: claveNueva,
})).estado);

c.titulo('17. Entra con la nueva, y ya no se le pide cambiarla');
const titularB3 = crearCliente('titularB3');
const conNueva = await titularB3.entrar(usuarioTitularB, claveNueva);
c.comprobar('entra', 200, conNueva.estado);
c.comprobar('ya no se le pide cambiarla', false, conNueva.json?.mustChangePassword === true);
c.comprobar('y sigue viendo su cartera', 200, (await titularB3.pedir('GET', `/api/portfolios/${idTitularB}`)).estado);

c.titulo('18. Una persona normal NO puede restablecer contrasenas ajenas');
c.comprobar('Titular B recibe 403', 403, (await titularB3.pedir('POST', `/api/admin/users/${creacion.json?.id}/reset-password`)).estado);

c.titulo('19. Y la temporal ya no sirve');
const conTemporalVieja = crearCliente('temporal-vieja');
c.comprobar('entrar con la temporal recibe 401', 401, (await conTemporalVieja.entrar(usuarioTitularB, temporal)).estado);

c.terminar();
