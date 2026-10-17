import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Csrf } from './csrf';

/** Metodos que NO cambian nada, y por tanto no exigen testigo. */
const LECTURA = new Set(['GET', 'HEAD', 'OPTIONS']);

/**
 * Manda la cookie de sesion en todas las peticiones.
 *
 * Con el proxy de desarrollo y con Caddy en produccion, la aplicacion y la API comparten origen, y en ese
 * caso el navegador manda las cookies solo. Se pone igualmente porque **deja escrito el requisito**: si
 * algun dia la API vive en otro origen, sin esto la sesion dejaria de viajar y el fallo apareceria como
 * un inexplicable «no estoy dentro» al recargar.
 *
 * Se hace con un interceptor y no peticion a peticion para que no dependa de que alguien se acuerde.
 */
export const credencialesInterceptor: HttpInterceptorFn = (peticion, siguiente) =>
  siguiente(peticion.clone({ withCredentials: true }));

/**
 * Anade el testigo CSRF a toda peticion que cambie algo.
 *
 * Va en un interceptor, y no en cada llamada, porque **olvidarse en una sola** dejaria esa accion sin
 * proteccion y nadie lo notaria: funcionaria igual. Ponerlo aqui hace que la proteccion no dependa de
 * que alguien se acuerde.
 *
 * Las peticiones de lectura no lo llevan: pedir un testigo para leer solo anadiria una ida y vuelta.
 */
export const csrfInterceptor: HttpInterceptorFn = (peticion, siguiente) => {
  if (LECTURA.has(peticion.method)) {
    return siguiente(peticion);
  }

  const csrf = inject(Csrf);

  return from(csrf.asegurar()).pipe(
    switchMap((testigo) =>
      siguiente(peticion.clone({ setHeaders: { 'X-CSRF-TOKEN': testigo } })),
    ),
  );
};

/** El codigo que la API pone en `title` cuando el testigo no vale. */
export const CODIGO_CSRF = 'csrf.invalid';

/**
 * Traduce los errores de la API a algo que la aplicacion pueda usar.
 *
 * **La API devuelve codigos, no textos** (ADR-013): `auth.invalid_credentials`, no «usuario o
 * contrasena incorrectos». Aqui se conservan tal cual, y el texto lo pone la pantalla en el idioma de
 * la persona. Traducirlos en el servicio ataria los mensajes a un solo idioma.
 *
 * El caso que importa de verdad es el **testigo caducado**: si la API responde que el testigo no vale,
 * se olvida el que habia para que la siguiente peticion pida uno nuevo. Sin eso, la aplicacion se
 * quedaria atascada repitiendo el mismo testigo invalido en cada intento.
 */
export const erroresInterceptor: HttpInterceptorFn = (peticion, siguiente) => {
  const csrf = inject(Csrf);

  return siguiente(peticion).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && codigoDe(error) === CODIGO_CSRF) {
        csrf.guardar(null);
      }

      return throwError(() => error);
    }),
  );
};

/**
 * El codigo de error que manda la API, o nulo si la respuesta no tenia esa forma.
 *
 * La API responde con `ProblemDetails` (RFC 9457) y pone el codigo en `title`.
 */
export function codigoDe(error: HttpErrorResponse): string | null {
  const cuerpo: unknown = error.error;

  if (cuerpo && typeof cuerpo === 'object' && 'title' in cuerpo) {
    const titulo = (cuerpo as { title?: unknown }).title;
    return typeof titulo === 'string' && titulo.length > 0 ? titulo : null;
  }

  return null;
}
