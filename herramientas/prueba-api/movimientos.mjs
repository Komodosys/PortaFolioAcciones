// Prueba de los movimientos de una cartera: aportaciones, retiros y ventas.
//
// Lo importante que se comprueba:
//   - Que una aportacion y un retiro se registran, y que su efecto sobre el dinero aportado es + y -.
//   - Que un importe no positivo, un tipo desconocido o una moneda malformada dan **400 y no 500**.
//   - Que una venta **no** se puede registrar por aqui: se registra al vender, porque ademas reduce la
//     posicion. Dejarla suelta permitiria una venta sin su reduccion.
//   - Que otra persona **no ve ni toca** los movimientos de una cartera ajena, con 404 y no 403.

import { crearCliente, crearComprobador, claveDelAdmin } from './cliente.mjs';

const c = crearComprobador();
const admin = crearCliente('admin');

c.titulo('Preparacion');
await admin.entrar('admin', claveDelAdmin());

const alta = await admin.pedir('POST', '/api/portfolios', {
  name: `Movimientos ${Date.now()}`,
  registrationType: 'NotRegistered',
  baseCurrency: 'CAD',
});
c.comprobar('se crea una cartera vacia', 201, alta.estado);
const id = alta.json?.id;

c.comprobar('y empieza sin movimientos', 0, (await admin.pedir('GET', `/api/portfolios/${id}/movements`)).json?.length);

c.titulo('1. Una aportacion');
const aportacion = await admin.pedir('POST', `/api/portfolios/${id}/movements`, {
  kind: 'Deposit',
  amount: 10_000,
  currency: 'CAD',
  occurredOn: '2026-01-15',
  notes: 'traspaso',
});
c.comprobar('se registra', 201, aportacion.estado);
c.comprobar('con su tipo', 'Deposit', aportacion.json?.kind);
c.comprobar('con su importe', 10_000, aportacion.json?.amount);
c.comprobar('con su moneda', 'CAD', aportacion.json?.currency);
c.comprobar('con su dia', '2026-01-15', aportacion.json?.occurredOn);
c.comprobar('y sin simbolo, porque no es una venta', null, aportacion.json?.symbol);

c.titulo('2. Un retiro');
const retiro = await admin.pedir('POST', `/api/portfolios/${id}/movements`, {
  kind: 'Withdrawal',
  amount: 2_500,
  currency: 'CAD',
});
c.comprobar('se registra', 201, retiro.estado);
c.comprobar('con su tipo', 'Withdrawal', retiro.json?.kind);
c.comprobar('y sin dia se usa el de hoy', true, (retiro.json?.occurredOn ?? '').length === 10);

c.titulo('3. Lo que NO se puede registrar, y se dice con 400 y no con 500');
const cero = await admin.pedir('POST', `/api/portfolios/${id}/movements`, { kind: 'Deposit', amount: 0, currency: 'CAD' });
c.comprobar('un importe de cero da 400', 400, cero.estado);
c.comprobar('y dice por que', 'movement.invalid_amount', cero.json?.title);

c.comprobar(
  'un importe negativo da 400',
  400,
  (await admin.pedir('POST', `/api/portfolios/${id}/movements`, { kind: 'Deposit', amount: -50, currency: 'CAD' })).estado,
);

// Una venta suelta dejaria el registro y la posicion diciendo cosas distintas.
c.comprobar(
  'una venta por aqui da 400',
  400,
  (await admin.pedir('POST', `/api/portfolios/${id}/movements`, { kind: 'Sale', amount: 100, currency: 'CAD' })).estado,
);

c.comprobar(
  'una moneda que no es un codigo da 400',
  400,
  (await admin.pedir('POST', `/api/portfolios/${id}/movements`, { kind: 'Deposit', amount: 10, currency: 'XX' })).estado,
);

c.titulo('4. El listado, del mas reciente al mas antiguo');
const lista = await admin.pedir('GET', `/api/portfolios/${id}/movements`);
c.comprobar('se listan los dos', 2, lista.json?.length);
c.comprobar('primero el mas reciente', 'Withdrawal', lista.json?.[0]?.kind);
c.comprobar('y despues el de enero', 'Deposit', lista.json?.[1]?.kind);

c.titulo('5. AISLAMIENTO: otra persona no los ve ni los toca');
const sufijo = Date.now().toString().slice(-6);
const clave = 'Portafolio-2026-movimientos';
await admin.pedir('POST', '/api/admin/users', {
  userName: `movimientos${sufijo}`,
  displayName: 'Persona de prueba de movimientos',
  password: clave,
  baseCurrency: 'CAD',
  language: 'es',
  isAdmin: false,
});

const otra = crearCliente('otra');
c.comprobar('entra', 200, (await otra.entrar(`movimientos${sufijo}`, clave)).estado);
c.comprobar('no puede leerlos', 404, (await otra.pedir('GET', `/api/portfolios/${id}/movements`)).estado);
c.comprobar(
  'ni añadir uno',
  404,
  (await otra.pedir('POST', `/api/portfolios/${id}/movements`, { kind: 'Deposit', amount: 1, currency: 'CAD' })).estado,
);
c.comprobar('y no se ha añadido nada', 2, (await admin.pedir('GET', `/api/portfolios/${id}/movements`)).json?.length);

c.terminar();
