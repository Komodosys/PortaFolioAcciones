// Cliente HTTP para las pruebas de la API, compartido por todas.
//
// Vive aparte porque las pruebas van a ser varias (aislamiento, instrumentos, posiciones, la vista de
// cartera) y cada una necesita lo mismo: sesion con cookies, testigo CSRF y contar comprobaciones.
// Copiado en cada fichero, el dia que cambie algo habria que acordarse de cambiarlo en todas.

export const BASE = 'http://localhost:8080';

/**
 * Un cliente con su propia sesión y su propio testigo CSRF.
 */
export function crearCliente(nombre) {
  const cookies = new Map();

  return {
    nombre,

    async pedir(metodo, ruta, cuerpo) {
      const cabeceras = {};

      if (cookies.size > 0) {
        cabeceras.cookie = [...cookies].map(([k, v]) => `${k}=${v}`).join('; ');
      }

      if (cuerpo !== undefined) {
        cabeceras['content-type'] = 'application/json';
      }

      // El testigo CSRF va en la cabecera: es el patron de doble envio.
      if (this.testigo && metodo !== 'GET') {
        cabeceras['x-csrf-token'] = this.testigo;
      }

      const respuesta = await fetch(`${BASE}${ruta}`, {
        method: metodo,
        headers: cabeceras,
        body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
      });

      // El servidor manda varias cabeceras set-cookie; hay que quedarse con todas.
      for (const una of (respuesta.headers.getSetCookie?.() ?? [])) {
        const [par] = una.split(';');
        const corte = par.indexOf('=');
        cookies.set(par.slice(0, corte), par.slice(corte + 1));
      }

      const texto = await respuesta.text();
      let json = null;
      try { json = texto ? JSON.parse(texto) : null; } catch { /* respuesta sin JSON */ }

      return { estado: respuesta.status, json, texto };
    },

    async obtenerTestigo() {
      const r = await this.pedir('GET', '/api/auth/csrf');
      this.testigo = r.json?.token ?? null;
      return this.testigo;
    },

    /**
     * Inicia sesión.
     *
     * **No lanza excepción si falla: devuelve la respuesta.** Quien la llama tiene que mirar
     * `respuesta.estado`, porque si no, un inicio de sesión fallido pasa desapercibido y el cliente sigue
     * haciendo peticiones sin sesión — que fallan más adelante con un 401 que no se parece a la causa.
     */
    async entrar(usuario, contrasena) {
      await this.obtenerTestigo();
      const respuesta = await this.pedir('POST', '/api/auth/login', { userName: usuario, password: contrasena });

      // El testigo CSRF esta ligado a quien lo pide, asi que el de antes de entrar ya no vale. El
      // servidor devuelve uno nuevo en la propia respuesta.
      if (respuesta.json?.csrfToken) {
        this.testigo = respuesta.json.csrfToken;
      }

      return respuesta;
    },
  };
}

/**
 * Cuenta comprobaciones y fallos. Devuelve el codigo de salida al final.
 */
export function crearComprobador() {
  const estado = { comprobaciones: 0, fallos: 0 };

  return {
    comprobar(descripcion, esperado, obtenido) {
      estado.comprobaciones++;
      const ok = esperado === obtenido;

      if (!ok) { estado.fallos++; }
      console.log(`  [${ok ? 'OK ' : 'MAL'}] ${descripcion}: esperado ${esperado}, obtenido ${obtenido}`);
    },

    titulo(texto) {
      console.log();
      console.log(texto);
    },

    nota(texto) {
      console.log(`     ${texto}`);
    },

    terminar() {
      console.log();
      console.log(`${estado.comprobaciones} comprobaciones, ${estado.fallos} fallos.`);
      console.log(estado.fallos === 0 ? 'RESULTADO: correcto.' : 'RESULTADO: HAY FALLOS.');
      process.exit(estado.fallos === 0 ? 0 : 1);
    },
  };
}

/** La contrasena del administrador, que llega por variable de entorno. */
export function claveDelAdmin() {
  const clave = process.env.ADMIN_PASSWORD;

  if (!clave) {
    console.error('Falta ADMIN_PASSWORD.');
    process.exit(1);
  }

  return clave;
}
