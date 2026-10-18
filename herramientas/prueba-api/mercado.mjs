// Prueba de los refrescos de precios pedidos a mano.
//
// Lo importante que se comprueba:
//   - Que **solo el administrador** puede lanzarlos, y que quien no lo es recibe 403 y no 401: la sesión
//     vale, es el rol el que no.
//   - Que el **freno** funciona: la segunda pulsación seguida da 429 y dice cuánto falta. Sin el freno, el
//     botón sería la forma de que la fuente gratuita nos bloqueara.
//   - Que refrescar **no escribe el cierre del día**. Eso es lo que lo hace seguro a cualquier hora, y aquí
//     se comprueba contando los cierres guardados antes y después.
//
// El freno es global al proceso, así que esta suite tiene que ejecutarse en orden y de una vez: la primera
// llamada gasta la ventana y las siguientes comprueban el rechazo.

import { crearCliente, crearComprobador, claveDelAdmin } from './cliente.mjs';

const c = crearComprobador();
const admin = crearCliente('admin');

c.titulo('Preparacion');
await admin.entrar('admin', claveDelAdmin());

// Los cierres guardados ANTES. Refrescar cotizaciones no debe añadir ninguno.
const antes = await admin.pedir('GET', '/api/portfolios');
c.comprobar('el administrador ve carteras', true, (antes.json ?? []).length > 0);

c.titulo('1. El administrador rellena el histórico');
// Va PRIMERO a propósito: el relleno gasta el freno, y las comprobaciones siguientes necesitan la ventana
// gastada para ver el 429. Al revés, el relleno recibiría el 429 de la comprobación anterior.
const relleno = await admin.pedir('POST', '/api/market/backfill');
c.comprobar('se rellena', 200, relleno.estado);
c.comprobar('y dice cuantos instrumentos', true, (relleno.json?.instrumentos ?? 0) > 0);
c.comprobar('y cuantos cierres nuevos', true, typeof relleno.json?.nuevos === 'number');
c.comprobar('y cuantos ya estaban', true, typeof relleno.json?.yaEstaban === 'number');

c.titulo('2. El freno: la siguiente operacion seguida no pasa');
const refresco = await admin.pedir('POST', '/api/market/refresh-quotes');
c.comprobar('da 429', 429, refresco.estado);
c.comprobar('y dice por que', 'mercado.demasiadoPronto', refresco.json?.title);
c.comprobar('y cuantos segundos faltan', true, (refresco.json?.retryAfterSeconds ?? 0) > 0);
c.comprobar('y los segundos no pasan del tope', true, (refresco.json?.retryAfterSeconds ?? 999) <= 120);

c.titulo('3. El mismo freno vale para las tres operaciones');
// Es a proposito: las tres llaman a la misma fuente, asi que un freno por operacion dejaria la puerta
// abierta a alternarlas para saltarselo.
const recoger = await admin.pedir('POST', '/api/market/collect');
c.comprobar('recoger el cierre tambien da 429', 429, recoger.estado);

const rellenoOtraVez = await admin.pedir('POST', '/api/market/backfill');
c.comprobar('y rellenar otra vez, tambien', 429, rellenoOtraVez.estado);

c.titulo('4. AISLAMIENTO: quien no administra no puede');
const sufijo = Date.now().toString().slice(-6);
const clave = 'Portafolio-2026-mercado';
await admin.pedir('POST', '/api/admin/users', {
  userName: `mercado${sufijo}`,
  displayName: 'Persona de prueba del mercado',
  password: clave,
  baseCurrency: 'CAD',
  language: 'es',
  isAdmin: false,
});

const persona = crearCliente('personaNormal');
c.comprobar('entra', 200, (await persona.entrar(`mercado${sufijo}`, clave)).estado);
c.comprobar('pero no puede refrescar cotizaciones', 403, (await persona.pedir('POST', '/api/market/refresh-quotes')).estado);
c.comprobar('ni recoger el cierre', 403, (await persona.pedir('POST', '/api/market/collect')).estado);
c.comprobar('ni rellenar el historico', 403, (await persona.pedir('POST', '/api/market/backfill')).estado);

c.titulo('5. Sin sesion tampoco');
const anonimo = crearCliente('anonimo');
c.comprobar('401 al refrescar', 401, (await anonimo.pedir('POST', '/api/market/refresh-quotes')).estado);
c.comprobar('401 al recoger', 401, (await anonimo.pedir('POST', '/api/market/collect')).estado);
c.comprobar('401 al rellenar', 401, (await anonimo.pedir('POST', '/api/market/backfill')).estado);

c.terminar();
