// El despliegue con Docker Compose: lo que se abre en el navegador de casa.
//
// Apunta a http://localhost:8080 y NO al servidor de desarrollo. Es la unica prueba que mira la pila tal
// como la usa la familia: Caddy sirviendo la aplicacion y pasando la API.
//
// Existe porque el despliegue estaba ROTO y no se notaba: `/` respondia 200 con el cuerpo vacio. La API
// funcionaba, la comprobacion de salud decia que todo estaba bien, y la aplicacion no existia. Solo se veia
// al abrirla en el navegador — que es justo lo que nadie hace cuando las pruebas pasan.

const BASE = process.env.DESPLIEGUE ?? 'http://localhost:8080';

let fallos = 0;

function comprobar(descripcion, esperado, obtenido) {
  const ok = esperado === obtenido;
  if (!ok) { fallos++; }
  console.log(`  [${ok ? 'OK ' : 'MAL'}] ${descripcion}: esperado ${esperado}, obtenido ${obtenido}`);
}

function titulo(texto) {
  console.log();
  console.log(texto);
}

/** Pide una ruta y devuelve el estado, el texto y las cabeceras. */
async function pedir(ruta, opciones = {}) {
  const r = await fetch(`${BASE}${ruta}`, opciones);

  return { estado: r.status, texto: await r.text(), cabeceras: r.headers };
}

// ---------------------------------------------------------------------------------------------
titulo('1. La aplicacion se sirve');

const raiz = await pedir('/');
comprobar('/ responde', 200, raiz.estado);
comprobar('y trae la aplicacion, no el cuerpo vacio', true, raiz.texto.includes('<app-root>'));
comprobar('con su titulo', true, raiz.texto.includes('PortaFolioAcciones'));

// ---------------------------------------------------------------------------------------------
titulo('2. Recargar en una ruta profunda sirve la aplicacion, no un 404');

// Es una aplicacion de una sola pagina: `/carteras` no existe como fichero, la resuelve el navegador. Sin
// el respaldo a `index.html`, guardar un enlace o recargar en esa pantalla daria 404.
for (const ruta of ['/carteras', '/preferencias', '/posiciones/00000000-0000-0000-0000-000000000000']) {
  const r = await pedir(ruta);
  comprobar(`${ruta} responde`, 200, r.estado);
  comprobar(`y sirve la aplicacion`, true, r.texto.includes('<app-root>'));
}

// ---------------------------------------------------------------------------------------------
titulo('3. Las traducciones llegan y NO se cachean');

// Si se cachearan, despues de actualizar la aplicacion seguiria enseñando los textos viejos: no llevan
// identificador en el nombre, asi que el navegador no tiene forma de saber que han cambiado.
for (const idioma of ['es', 'fr', 'en']) {
  const r = await pedir(`/i18n/${idioma}.json`);
  comprobar(`${idioma}.json responde`, 200, r.estado);
  comprobar(`${idioma}.json no se cachea`, 'no-cache', r.cabeceras.get('cache-control'));

  try {
    JSON.parse(r.texto);
    comprobar(`${idioma}.json es JSON valido`, true, true);
  } catch {
    comprobar(`${idioma}.json es JSON valido`, true, false);
  }
}

// ---------------------------------------------------------------------------------------------
titulo('4. La API sigue llegando por la porteria');

const salud = await pedir('/salud');
comprobar('/salud responde', 200, salud.estado);
comprobar('y dice que la base de datos esta bien', true, salud.texto.includes('"status":"Healthy"'));

// El inicio de sesion, con el testigo: es el camino que hace el navegador de verdad.
const galletas = new Map();

async function conSesion(metodo, ruta, cuerpo, testigo) {
  const cabeceras = {};

  if (galletas.size > 0) {
    cabeceras.cookie = [...galletas].map(([k, v]) => `${k}=${v}`).join('; ');
  }

  if (cuerpo !== undefined) { cabeceras['content-type'] = 'application/json'; }
  if (testigo) { cabeceras['x-csrf-token'] = testigo; }

  const r = await fetch(`${BASE}${ruta}`, {
    method: metodo,
    headers: cabeceras,
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
  });

  for (const una of (r.headers.getSetCookie?.() ?? [])) {
    const [par] = una.split(';');
    const corte = par.indexOf('=');
    galletas.set(par.slice(0, corte), par.slice(corte + 1));
  }

  const texto = await r.text();
  let json = null;
  try { json = texto ? JSON.parse(texto) : null; } catch { /* sin JSON */ }

  return { estado: r.status, json };
}

const csrf = await conSesion('GET', '/api/auth/csrf');
comprobar('/api/auth/csrf responde', 200, csrf.estado);

const clave = process.env.ADMIN_PASSWORD;
const entrada = await conSesion('POST', '/api/auth/login', { userName: 'admin', password: clave }, csrf.json?.token);

comprobar('el inicio de sesion funciona por la porteria', 200, entrada.estado);

const perfil = await conSesion('GET', '/api/auth/me');
comprobar('y la sesion se mantiene', 200, perfil.estado);
comprobar('con el perfil de administrador', true, perfil.json?.isAdmin === true);

// ---------------------------------------------------------------------------------------------
titulo('5. Una ruta de la API que no existe NO devuelve la aplicacion');

// Es la otra cara del respaldo a `index.html`: si se aplicara tambien a `/api`, un error de la API llegaria
// al cliente como una pagina HTML con un 200, y el cliente intentaria leerla como datos.
const inexistente = await pedir('/api/no-existe');
comprobar('responde 404', 404, inexistente.estado);
comprobar('y no devuelve HTML de la aplicacion', false, inexistente.texto.includes('<app-root>'));

// ---------------------------------------------------------------------------------------------
console.log();
console.log(`${fallos === 0 ? 'RESULTADO: el despliegue sirve.' : `RESULTADO: ${fallos} FALLO(S).`}`);
process.exit(fallos === 0 ? 0 : 1);
