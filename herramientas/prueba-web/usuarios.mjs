// Las cuentas de la casa: quien puede entrar y quien puede administrar.
//
// Lo que se comprueba aqui es lo que de verdad importa de esta pantalla, y no es que se vea bonita:
//
//   1. **Un administrador ve las cuentas; uno que no lo es, NO.** Ni por la pantalla ni, sobre todo, por la
//      API: la guarda de la ruta es comodidad, la proteccion esta en el servidor.
//   2. **Una cuenta recien creada puede entrar** con la clave temporal que se le entrega.
//   3. **Y no ve nada de nadie.** Empieza sin carteras, y pedir la de otra persona por su direccion no
//      funciona: 404 y no 403, para no confirmar siquiera que existe.
//   4. La cuenta queda marcada con la **clave temporal sin cambiar**, que es lo que distingue una cuenta
//      recien creada de una que se usa.
//
// **Deja cuentas en la base de datos.** No hay forma de borrarlas desde la API —no existe la operacion, y es
// una carencia real—, asi que cada ejecucion añade una. Por eso el nombre lleva un sufijo unico: se puede
// ejecutar muchas veces sin que choque. Para dejar la base limpia hay que vaciarla y volver a cargar, como
// dice el README.

import { crearCliente, crearComprobador, claveDelAdmin } from '../prueba-api/cliente.mjs';

const { comprobar, titulo, nota, terminar } = crearComprobador();

const admin = crearCliente('admin');
await admin.entrar('admin', claveDelAdmin());

/** Un nombre de entrada libre, para poder ejecutar la prueba mas de una vez. */
const sufijo = Date.now().toString(36);
const nuevoUsuario = `prueba.${sufijo}`;
const nuevaPersona = `Persona de prueba ${sufijo}`;

// ---------------------------------------------------------------------------------------------
titulo('1. El administrador ve las cuentas');

const lista = await admin.pedir('GET', '/api/admin/users');

comprobar('la lista responde', 200, lista.estado);

const cuentas = lista.json ?? [];

comprobar('hay al menos el administrador', true, cuentas.some((c) => c.userName === 'admin'));
comprobar('y las demas personas de la casa', true, cuentas.length >= 2);

// Lo que la pantalla enseña de cada cuenta, para poder decidir si hay que volver a dar una clave.
const primera = cuentas[0];

for (const campo of ['userName', 'displayName', 'baseCurrency', 'language', 'isAdmin', 'mustChangePassword']) {
  comprobar(`cada cuenta trae '${campo}'`, true, campo in primera);
}

nota(`${cuentas.length} cuentas`);

// ---------------------------------------------------------------------------------------------
titulo('2. Se crea una cuenta y se le entrega una clave temporal');

const alta = await admin.pedir('POST', '/api/admin/users', {
  userName: nuevoUsuario,
  displayName: nuevaPersona,
  // La pantalla manda una provisional al azar y no la usa nadie: acto seguido se pide la temporal.
  password: `provisional-${sufijo}-Aa1!`,
  baseCurrency: 'CAD',
  language: 'es',
  isAdmin: false,
});

comprobar('la cuenta se crea', 201, alta.estado);
comprobar('y devuelve su identificador', true, typeof alta.json?.id === 'string');

const temporal = await admin.pedir('POST', `/api/admin/users/${alta.json.id}/reset-password`, {});

comprobar('se genera la clave temporal', 200, temporal.estado);
comprobar('y viene en la respuesta', true, typeof temporal.json?.temporaryPassword === 'string');
comprobar('marcada como pendiente de cambiar', true, temporal.json?.mustChangePassword === true);

const claveTemporal = temporal.json.temporaryPassword;

// La provisional NO sirve: se sustituyo. Es lo que hace que no importe que se haya mandado al crear.
const conProvisional = crearCliente('provisional');
const intentoConProvisional = await conProvisional.entrar(nuevoUsuario, `provisional-${sufijo}-Aa1!`);

comprobar('la clave provisional NO sirve', 401, intentoConProvisional.estado);

// ---------------------------------------------------------------------------------------------
titulo('3. La cuenta nueva puede entrar, y no ve nada de nadie');

const nueva = crearCliente('nueva');
const entrada = await nueva.entrar(nuevoUsuario, claveTemporal);

comprobar('entra con la temporal', 200, entrada.estado);

// El `isAdmin` no viene en la respuesta del inicio de sesión: se pregunta por el perfil, que es donde está.
const perfil = await nueva.pedir('GET', '/api/auth/me');

comprobar('y no es administrador', false, perfil.json?.isAdmin);

const suyas = await nueva.pedir('GET', '/api/portfolios');

comprobar('su lista de carteras responde', 200, suyas.estado);
comprobar('y esta VACIA: no ve las de los demas', 0, (suyas.json ?? []).length);

// Y pedir una cartera ajena por su direccion tampoco: 404, no 403. Un 403 confirmaria que existe.
const ajena = cuentas.find((c) => true);
const carterasDelAdmin = await admin.pedir('GET', '/api/portfolios');
const unaAjena = (carterasDelAdmin.json ?? [])[0];

if (unaAjena) {
  const intento = await nueva.pedir('GET', `/api/portfolios/${unaAjena.id}/view`);

  comprobar('pedir la cartera de otra persona da 404, no 403', 404, intento.estado);
} else {
  nota('no hay carteras para probar el acceso a una ajena');
}

// ---------------------------------------------------------------------------------------------
titulo('4. Y NO puede administrar');

// Esta es la comprobacion de verdad de esta pantalla: la guarda de la ruta solo esconde el enlace.
const intentoDeLista = await nueva.pedir('GET', '/api/admin/users');

comprobar('no puede ver la lista de cuentas', 403, intentoDeLista.estado);

const intentoDeAlta = await nueva.pedir('POST', '/api/admin/users', {
  userName: `colado.${sufijo}`,
  displayName: 'Colado',
  password: 'intento-Aa1!xxxxx',
  baseCurrency: 'CAD',
  language: 'es',
  isAdmin: true,
});

comprobar('no puede crear cuentas', 403, intentoDeAlta.estado);
comprobar('y menos aun crear un administrador', true, intentoDeAlta.estado >= 400);

const intentoDeClave = await nueva.pedir('POST', `/api/admin/users/${alta.json.id}/reset-password`, {});

comprobar('no puede generar claves temporales', 403, intentoDeClave.estado);

// ---------------------------------------------------------------------------------------------
titulo('5. La cuenta nueva aparece marcada con la clave sin cambiar');

const trasCrear = await admin.pedir('GET', '/api/admin/users');
const creada = (trasCrear.json ?? []).find((c) => c.userName === nuevoUsuario);

comprobar('la cuenta esta en la lista', true, creada !== undefined);
comprobar('con su nombre', nuevaPersona, creada?.displayName);
comprobar('y con la clave temporal pendiente', true, creada?.mustChangePassword === true);
comprobar('y no como administrador', false, creada?.isAdmin);

nota(`${creada?.userName} · ${creada?.displayName} · ${creada?.baseCurrency} · ${creada?.language}`);

// ---------------------------------------------------------------------------------------------
titulo('6. Sin sesion, nada');

const anonimo = crearCliente('anonimo');
const sinSesion = await anonimo.pedir('GET', '/api/admin/users');

comprobar('sin sesion responde 401', 401, sinSesion.estado);

terminar();
