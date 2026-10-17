import { Routes } from '@angular/router';
import { administradorGuard, sesionGuard } from './core/sesion/guardas';

/**
 * Las rutas de la aplicacion.
 *
 * Todo lo que no sea entrar vive **dentro del marco**, que es quien pinta la cabecera. Asi la cabecera no
 * se repite en cada pantalla ni se olvida en ninguna.
 *
 * Las pantallas se cargan **a demanda**: la de administracion la usa una sola persona de la casa, y no
 * tiene sentido que las demas se descarguen su codigo.
 */
export const routes: Routes = [
  {
    path: 'entrar',
    loadComponent: () => import('./features/acceso/entrar/entrar').then((m) => m.Entrar),
  },
  {
    path: 'cambiar-contrasena',
    canActivate: [sesionGuard],
    loadComponent: () =>
      import('./features/acceso/cambiar-contrasena/cambiar-contrasena').then(
        (m) => m.CambiarContrasena,
      ),
  },
  {
    path: '',
    canActivate: [sesionGuard],
    loadComponent: () => import('./shared/marco/marco').then((m) => m.Marco),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'carteras' },
      {
        path: 'carteras',
        loadComponent: () =>
          import('./features/carteras/lista/lista').then((m) => m.ListaDeCarteras),
      },
      {
        path: 'carteras/:id',
        loadComponent: () =>
          import('./features/carteras/detalle/detalle').then((m) => m.DetalleDeCartera),
      },
      {
        path: 'carteras/:id/nueva-posicion',
        loadComponent: () =>
          import('./features/posiciones/nueva/nueva').then((m) => m.NuevaPosicion),
      },
      {
        path: 'posiciones/:id',
        loadComponent: () =>
          import('./features/posiciones/detalle/detalle').then((m) => m.DetalleDePosicion),
      },
      {
        path: 'preferencias',
        loadComponent: () =>
          import('./features/preferencias/preferencias').then((m) => m.Preferencias),
      },
      {
        // La guarda va en la RUTA y no solo en el enlace del menu: esconder un enlace no protege nada, y
        // la direccion se puede escribir a mano. La API lo comprueba igualmente, pero asi la persona que
        // no administra ve una pantalla de «no puedes» en vez de una pantalla vacia.
        path: 'administracion/auditoria',
        canActivate: [administradorGuard],
        loadComponent: () =>
          import('./features/administracion/auditoria/auditoria').then((m) => m.Auditoria),
      },
      {
        // Aqui se crean accesos a los datos de la familia y se generan credenciales, asi que la guarda es
        // mas importante que en ninguna otra pantalla. Aun asi, la proteccion de verdad es la API: esto
        // solo evita enseñar lo que no va a funcionar.
        path: 'administracion/usuarios',
        canActivate: [administradorGuard],
        loadComponent: () =>
          import('./features/administracion/usuarios/usuarios').then((m) => m.Usuarios),
      },
    ],
  },

  // Cualquier direccion desconocida lleva a la portada, en vez de dejar una pantalla en blanco.
  { path: '**', redirectTo: '' },
];
