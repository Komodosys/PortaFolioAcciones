// Comprueba que el problema es el NOMBRE DE USUARIO y no la contrasena.
//
// Si `admin` entra y `ADMIN_PASSWORD` no, entonces la contrasena era la correcta desde el principio y lo
// unico que fallaba era haber escrito el nombre de la variable en lugar del usuario.

const BASE = 'http://localhost:8080';

async function intentar(usuario, clave) {
  const cookies = new Map();

  const r1 = await fetch(`${BASE}/api/auth/csrf`);
  for (const u of (r1.headers.getSetCookie?.() ?? [])) {
    const [a] = u.split(';');
    const c = a.indexOf('=');
    cookies.set(a.slice(0, c), a.slice(c + 1));
  }

  const testigo = (await r1.json()).token;

  const r2 = await fetch(`${BASE}/api/auth/login`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      cookie: [...cookies].map(([k, v]) => `${k}=${v}`).join('; '),
      'x-csrf-token': testigo,
    },
    body: JSON.stringify({ userName: usuario, password: clave }),
  });

  return r2.status;
}

const clave = process.env.ADMIN_PASSWORD;

const casos = [
  ['admin', clave, 'el usuario de verdad'],
  ['ADMIN_PASSWORD', clave, 'el NOMBRE de la variable, escrito como usuario'],
  ['Admin', clave, 'el usuario con mayuscula'],
  ['admin', 'equivocada', 'la contrasena mal, para comparar'],
];

for (const [usuario, contrasena, descripcion] of casos) {
  const estado = await intentar(usuario, contrasena);
  const veredicto = estado === 200 ? 'ENTRA' : 'rechazado';
  console.log(`  ${String(estado).padEnd(4)} ${veredicto.padEnd(10)} ${usuario.padEnd(16)} (${descripcion})`);
}
