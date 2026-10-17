import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Sesion } from './sesion';

/**
 * Deja pasar solo si hay sesion.
 *
 * Antes de decidir, **intenta recuperar la sesion**: recargar la pagina no debe echar a nadie fuera
 * aunque su cookie siga viva. La recuperacion se hace una sola vez, dentro del propio servicio, asi que
 * navegar entre pantallas no repite la peticion.
 *
 * No devuelve `false`, devuelve un `UrlTree` hacia la pantalla de entrada: con `false` la direccion se
 * quedaria como estaba y la persona veria una pantalla en blanco sin entender por que.
 */
export const sesionGuard: CanActivateFn = async () => {
  const sesion = inject(Sesion);
  const router = inject(Router);

  await sesion.restaurar();

  return sesion.haySesion() ? true : router.createUrlTree(['/entrar']);
};

/**
 * Deja pasar solo al administrador.
 *
 * Se comprueba aqui **ademas de en la API**, y no en lugar de: esta guarda es comodidad —no enseñar una
 * pantalla que no va a funcionar—, no seguridad. Quien manda de verdad es el servidor, que rechaza la
 * peticion aunque alguien llegue a la pantalla por su cuenta.
 */
export const administradorGuard: CanActivateFn = async () => {
  const sesion = inject(Sesion);
  const router = inject(Router);

  await sesion.restaurar();

  if (!sesion.haySesion()) {
    return router.createUrlTree(['/entrar']);
  }

  return sesion.esAdministrador() ? true : router.createUrlTree(['/carteras']);
};
