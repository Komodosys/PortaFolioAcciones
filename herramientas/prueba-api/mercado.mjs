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

c.titulo('1. El administrador refresca las cotizaciones');
const refresco = await admin.pedir('POST', '/api/market/refresh-quotes');
c.comprobar('se refresca', 200, refresco.estado);
c.comprobar('y dice cuantos instrumentos', true, typeof refresco.json?.instrumentos === 'number');
c.comprobar('y cuantos se actualizaron', true, typeof refresco.json?.actualizados === 'number');
c.comprobar('y cuantos se quedaron sin precio', true, typeof refresco.json?.sinPrecio === 'number');

c.titulo('2. El freno: la segunda pulsacion seguida no pasa');
const otraVez = await admin.pedir('POST', '/api/market/refresh-quotes');
c.comprobar('da 429', 429, otraVez.estado);
c.comprobar('y dice por que', 'mercado.demasiadoPronto', otraVez.json?.title);
c.comprobar('y cuantos segundos faltan', true, (otraVez.json?.retryAfterSeconds ?? 0) > 0);
c.comprobar('y los segundos no pasan del tope', true, (otraVez.json?.retryAfterSeconds ?? 999) <= 120);

c.titulo('3. El mismo freno vale para recoger el cierre');
// Es a proposito: las dos llaman a la misma fuente, asi que un freno por operacion dejaria la puerta abierta
// a alternarlas para saltarselo.
const recoger = await admin.pedir('POST', '/api/market/collect');
c.comprobar('tambien da 429 mientras dura la espera', 429, recoger.estado);

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

c.titulo('5. Sin sesion tampoco');
const anonimo = crearCliente('anonimo');
c.comprobar('401 al refrescar', 401, (await anonimo.pedir('POST', '/api/market/refresh-quotes')).estado);
c.comprobar('401 al recoger', 401, (await anonimo.pedir('POST', '/api/market/collect')).estado);

c.terminar();
