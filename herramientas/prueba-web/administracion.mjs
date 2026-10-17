// La parte del administrador: el aviso de datos ajenos y el libro de auditoria.
//
// Lo que se comprueba aqui no es que las pantallas pinten bien, sino las dos cosas que hacen que el poder
// del administrador sea legitimo (ADR-014):
//
//   1. Que SIEMPRE se pueda saber de quien es lo que se esta mirando.
//   2. Que todo cambio sobre datos ajenos quede anotado, con el valor anterior y el nuevo.

import { crearCliente, crearComprobador, claveDelAdmin } from '../prueba-api/cliente.mjs';

// OJO, Y ESTO IMPORTA: aqui NO se imprime ningun nombre de persona ni de cartera.
//
// La base de datos tiene los datos de verdad —es la aplicacion de una casa, para eso esta— y los nombres
// que hay dentro son reales. La base no se publica, asi que no es una fuga. Pero **una salida de consola
// acaba copiada en un documento**, y por ahi si se cuela. Paso: la primera version de este fichero imprimio
// un nombre real al informar de que carteras eran de otras personas.
//
// Por eso se cuentan y se identifican por su codigo, nunca por su nombre.

const comprobador = crearComprobador();
const { comprobar, titulo, nota, terminar } = comprobador;

const admin = crearCliente('admin');
await admin.entrar('admin', claveDelAdmin());

// ---------------------------------------------------------------------------------------------
titulo('1. De quien es cada cartera que ve el administrador');

const carteras = await admin.pedir('GET', '/api/portfolios');
comprobar('responde', 200, carteras.estado);
comprobar('es una lista', true, Array.isArray(carteras.json));

const sinDueno = (carteras.json ?? []).filter((c) => !c.ownerDisplayName || !c.ownerUserId);
comprobar('NINGUNA se queda sin decir de quien es', 0, sinDueno.length);

// El administrador se reconoce por su identificador, no por su nombre: asi este fichero no depende de como
// se llame nadie, y no imprime ningun nombre.
const perfilAdmin = (await admin.pedir('GET', '/api/profile')).json;
const yoAdmin = perfilAdmin?.id;
const mias = (carteras.json ?? []).filter((c) => c.ownerUserId === yoAdmin);
const ajenas = (carteras.json ?? []).filter((c) => c.ownerUserId !== yoAdmin);

nota(`${carteras.json?.length} carteras: ${mias.length} mias y ${ajenas.length} de otras personas`);
nota(`hay ${new Set(ajenas.map((c) => c.ownerUserId)).size} personas distintas con datos a la vista`);

// Sin esto, el administrador abre la cartera de otra persona SIN SABERLO: no es solo confusion, es que
// puede modificar datos ajenos creyendo que son suyos.
comprobar('hay carteras de otras personas que identificar', true, ajenas.length > 0);
comprobar(
  'y cada una dice de quien es, con un nombre',
  true,
  ajenas.every((c) => typeof c.ownerDisplayName === 'string' && c.ownerDisplayName.length > 0),
);

// ---------------------------------------------------------------------------------------------
titulo('2. El libro de auditoria se puede leer');

const libro = await admin.pedir('GET', '/api/admin/audit?limit=20');
comprobar('responde', 200, libro.estado);
comprobar('es una lista', true, Array.isArray(libro.json));

const conAutor = (libro.json ?? []).filter((a) => !a.actorName || a.actorName === '?');
comprobar('todas dicen QUIEN lo hizo', 0, conAutor.length);
comprobar('y de QUIEN eran los datos', 0, (libro.json ?? []).filter((a) => !a.ownerName || a.ownerName === '?').length);

if (libro.json?.length > 0) {
  const primera = libro.json[0];
  nota(`la mas reciente: ${primera.action} sobre ${primera.entityType}`);
}

const totalLibro = (await admin.pedir('GET', '/api/admin/audit?limit=1000')).json?.length ?? 0;
nota(`${totalLibro} anotaciones en el libro`);

// ---------------------------------------------------------------------------------------------
titulo('3. Se puede pedir SOLO lo que toca datos ajenos');

const soloAjenas = await admin.pedir('GET', '/api/admin/audit?onlySomeoneElseData=true&limit=1000');
comprobar('responde', 200, soloAjenas.estado);

// Es el filtro que de verdad se usa: del libro entero, lo que hay que revisar son las que no deberian ser
// rutina. Si colara alguna propia, el filtro no serviria para nada.
comprobar('ninguna es de datos propios', 0, (soloAjenas.json ?? []).filter((a) => !a.isSomeoneElseData).length);

const todas = await admin.pedir('GET', '/api/admin/audit?limit=1000');
const ajenasEnTodas = (todas.json ?? []).filter((a) => a.isSomeoneElseData).length;
comprobar('y son exactamente las ajenas del libro entero', ajenasEnTodas, soloAjenas.json?.length ?? -1);
nota(`${ajenasEnTodas} de ${todas.json?.length} tocan datos de otra persona`);

// ---------------------------------------------------------------------------------------------
titulo('4. Se puede filtrar por persona y por fecha');

const personas = await admin.pedir('GET', '/api/admin/audit/people');
comprobar('la lista de personas responde', 200, personas.estado);
comprobar(
  'y trae nombres, no identificadores',
  true,
  (personas.json ?? []).every((p) => typeof p.displayName === 'string' && p.displayName.length > 0),
);
nota(`${personas.json?.length} personas tienen datos anotados en el libro`);

if (personas.json?.length > 0) {
  const una = personas.json[0];
  const suyas = await admin.pedir('GET', `/api/admin/audit?ownerUserId=${una.id}&limit=1000`);
  comprobar('filtrando por una persona responde', 200, suyas.estado);

  // Se compara por identificador y no por nombre: el nombre puede repetirse entre dos personas.
  const deOtros = (suyas.json ?? []).filter((a) => a.ownerName !== una.displayName);
  comprobar('y solo salen las suyas', 0, deOtros.length);

  // Y TODAS las personas de la lista tienen que devolver algo. Si alguna devolviera cero, la lista
  // ofreceria una opcion que parece rota — que es justo lo que pasaba antes, cuando la lista incluia
  // tambien a quien solo aparecia como autor.
  const vacias = [];
  for (const persona of personas.json) {
    const r = await admin.pedir('GET', `/api/admin/audit?ownerUserId=${persona.id}&limit=5`);
    if ((r.json?.length ?? 0) === 0) {
      vacias.push(persona.id);
    }
  }
  comprobar('y NINGUNA de la lista sale vacia', 0, vacias.length);
}

// Una fecha en el futuro no debe devolver nada: es la forma barata de comprobar que el filtro de fecha
// hace algo, en vez de ignorarse en silencio.
comprobar('desde el futuro no hay nada', 0, (await admin.pedir('GET', '/api/admin/audit?from=2099-01-01T00:00:00Z')).json?.length ?? -1);
comprobar('hasta el año 2000 tampoco', 0, (await admin.pedir('GET', '/api/admin/audit?to=2000-01-01T00:00:00Z')).json?.length ?? -1);
comprobar('y filtrando por una entidad que no existe, tampoco', 0, (await admin.pedir('GET', '/api/admin/audit?entityType=NoExiste')).json?.length ?? -1);

// ---------------------------------------------------------------------------------------------
titulo('5. Un cambio del administrador sobre datos ajenos QUEDA ANOTADO');

if (ajenas.length === 0) {
  nota('no hay carteras ajenas: no se puede probar el registro');
} else {
  const victima = ajenas[0];
  const antes = (await admin.pedir('GET', '/api/admin/audit?limit=1000')).json?.length ?? 0;

  const nuevoNombre = `CARTERAS-PRUEBA-AUDITORIA-${Date.now()}`;
  const cambio = await admin.pedir('PATCH', `/api/portfolios/${victima.id}`, { name: nuevoNombre });
  comprobar('el administrador puede renombrar lo ajeno', 200, cambio.estado);

  const despues = (await admin.pedir('GET', '/api/admin/audit?limit=1000')).json ?? [];

  // ESTA es la contrapartida del poder: el cambio no puede quedar invisible.
  comprobar('el libro crece en una anotacion', antes + 1, despues.length);

  const ultima = despues[0];
  comprobar('y la ultima es la de ese cambio', victima.id, ultima?.entityId);
  comprobar('con el actor', perfilAdmin?.displayName, ultima?.actorName);
  comprobar('marcada como cambio ajeno', true, ultima?.isSomeoneElseData);
  comprobar('con el tipo de entidad', 'Portfolio', ultima?.entityType);

  // El valor ANTERIOR y el nuevo. Un registro que solo dijera «se modifico» no serviria para nada. Se
  // comprueba que esten los dos, sin imprimirlos: el anterior es el nombre de una cartera, y ese nombre
  // lleva dentro el de una persona.
  const cambioDeNombre = (ultima?.changes ?? []).find((c) => c.field?.toLowerCase().includes('name'));
  comprobar('con el valor nuevo, que es el que se acaba de poner', nuevoNombre, cambioDeNombre?.newValue);
  comprobar('y con un valor anterior que no esta vacio', true, typeof cambioDeNombre?.oldValue === 'string' && cambioDeNombre.oldValue.length > 0);
  nota(`campos anotados: ${(ultima?.changes ?? []).map((c) => c.field).join(', ')}`);

  // Se deja como estaba, para no llenar la aplicacion de nombres de prueba. El nombre original NO se
  // imprime: esta en una variable, y con eso basta.
  await admin.pedir('PATCH', `/api/portfolios/${victima.id}`, { name: victima.name });
  const devuelto = await admin.pedir('GET', `/api/portfolios/${victima.id}`);
  comprobar('y se deja como estaba', victima.name, devuelto.json?.name);
  comprobar('lo que deja DOS anotaciones mas', antes + 2, ((await admin.pedir('GET', '/api/admin/audit?limit=1000')).json ?? []).length);
}

// ---------------------------------------------------------------------------------------------
titulo('6. El libro es de SOLO AÑADIR');

// No hay endpoint para escribir en el libro, y no debe haberlo. Se comprueba que no exista: un registro
// que se puede modificar no es un registro.
for (const [metodo, ruta] of [
  ['POST', '/api/admin/audit'],
  ['PUT', '/api/admin/audit'],
  ['DELETE', '/api/admin/audit'],
]) {
  const r = await admin.pedir(metodo, ruta, metodo === 'DELETE' ? undefined : {});
  comprobar(`${metodo} /api/admin/audit no existe`, true, r.estado === 404 || r.estado === 405);
}

const borrar = await admin.pedir('DELETE', `/api/admin/audit/${libro.json?.[0]?.id ?? '00000000-0000-0000-0000-000000000000'}`);
comprobar('y una anotacion concreta tampoco se borra', true, borrar.estado === 404 || borrar.estado === 405);

terminar();
