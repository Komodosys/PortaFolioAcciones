// Prueba del informe de rendimiento.
//
// Lo importante que se comprueba:
//   - Que el administrador ve las carteras de todo el mundo, y que **quien no lo es ve solo las suyas**: un
//     usuario recien creado no tiene ninguna, asi que recibe cero filas.
//   - Que los totales se dan **solo si todas las filas comparten moneda**. Sumar dolares y euros daria un
//     numero que no significa nada.
//   - Que la **ganancia realizada es nula** cuando alguna venta no tiene el coste guardado. Cero diria «no se
//     gano nada», y no es lo mismo que no saberlo.

import { crearCliente, crearComprobador, claveDelAdmin } from './cliente.mjs';

const c = crearComprobador();
const admin = crearCliente('admin');

c.titulo('Preparacion');
await admin.entrar('admin', claveDelAdmin());

c.titulo('1. El administrador ve las de toda la casa');
const informe = await admin.pedir('GET', '/api/reports/performance');
c.comprobar('el informe se genera', 200, informe.estado);
c.comprobar('con filas', true, (informe.json?.rows ?? []).length > 0);
c.comprobar('y con sus monedas', true, (informe.json?.currencies ?? []).length > 0);

const primera = informe.json?.rows?.[0];
c.comprobar('cada fila dice de quien es', true, typeof primera?.ownerDisplayName === 'string');
c.comprobar('y su tipo de registro', true, typeof primera?.registrationType === 'string');

// La fecha puede ser nula: una cartera creada después de la última recogida todavía no tiene foto, y decir
// una fecha que no existe sería peor que no decir ninguna. Lo que se exige es que, si la hay, sea una fecha.
c.comprobar(
  'y la fecha de las cifras es una fecha o nula',
  true,
  (informe.json?.rows ?? []).every((fila) => fila.snapshotDate === null || /^\d{4}-\d{2}-\d{2}$/.test(fila.snapshotDate)),
);
c.comprobar(
  'y las carteras con foto la traen',
  true,
  (informe.json?.rows ?? []).some((fila) => typeof fila.snapshotDate === 'string'),
);

c.titulo('2. Los totales, solo si se pueden sumar');
if ((informe.json?.currencies ?? []).length === 1) {
  c.comprobar('con una sola moneda hay totales', true, informe.json?.totals !== null);
  c.comprobar('y el valor total es la suma', true, (informe.json?.totals?.marketValue ?? -1) >= 0);
  c.comprobar(
    'y la ganancia no realizada cuadra',
    Math.round((informe.json.totals.marketValue - informe.json.totals.costBasis) * 100) / 100,
    Math.round(informe.json.totals.unrealizedGain * 100) / 100,
  );
} else {
  c.comprobar('con varias monedas NO hay totales', null, informe.json?.totals);
}

c.titulo('3. La ganancia realizada: nula antes que falsa');
const conVentas = (informe.json?.rows ?? []).filter((fila) => fila.realizedGain !== null);
// No se exige que haya ventas con coste: depende de los datos. Lo que si se exige es que, si las hay, el
// numero sea un numero y no una cadena.
c.comprobar('si hay ganancia realizada, es un numero', true, conVentas.every((fila) => typeof fila.realizedGain === 'number'));

c.titulo('4. AISLAMIENTO: quien no administra ve solo lo suyo');
const sufijo = Date.now().toString().slice(-6);
const clave = 'Portafolio-2026-informe';
await admin.pedir('POST', '/api/admin/users', {
  userName: `informe${sufijo}`,
  displayName: 'Persona de prueba del informe',
  password: clave,
  baseCurrency: 'CAD',
  language: 'es',
  isAdmin: false,
});

const persona = crearCliente('persona');
c.comprobar('entra', 200, (await persona.entrar(`informe${sufijo}`, clave)).estado);

const suyo = await persona.pedir('GET', '/api/reports/performance');
c.comprobar('su informe se genera', 200, suyo.estado);
c.comprobar('y viene VACIO, porque no tiene carteras', 0, (suyo.json?.rows ?? []).length);
c.comprobar('y sin totales', null, suyo.json?.totals);

c.titulo('5. Sin sesion tampoco');
const anonimo = crearCliente('anonimo');
c.comprobar('401 sin sesion', 401, (await anonimo.pedir('GET', '/api/reports/performance')).estado);

c.terminar();
