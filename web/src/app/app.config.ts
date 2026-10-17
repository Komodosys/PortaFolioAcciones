import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, isDevMode, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { TranslocoHttpLoader } from './core/i18n/transloco-loader';
import {
  credencialesInterceptor,
  csrfInterceptor,
  erroresInterceptor,
} from './core/api/interceptores';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    // `withComponentInputBinding` hace que los parametros de la direccion lleguen como señales al
    // componente. Asi, pasar de una cartera a otra **recarga sola**: sin esto habria que escuchar los
    // cambios de la direccion a mano en cada pantalla.
    provideRouter(routes, withComponentInputBinding()),

    // El orden de los interceptores importa: primero el de credenciales, que es el que hace que viaje la
    // cookie; despues el del testigo CSRF, que anade la cabecera; y por ultimo el de errores, que ya ve
    // la respuesta con todo puesto.
    provideHttpClient(
      withInterceptors([credencialesInterceptor, csrfInterceptor, erroresInterceptor]),
    ),

    // Multi-idioma en tiempo de ejecucion (ADR-013): el idioma es una preferencia de la persona,
    // no de la instalacion, y se cambia sin recargar.
    provideTransloco({
      config: {
        availableLangs: ['es', 'fr', 'en'],
        defaultLang: 'es',
        // Si falta una traduccion, se muestra en ingles en vez de dejar la clave a la vista.
        fallbackLang: 'en',
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
      },
      loader: TranslocoHttpLoader,
    }),
  ],
};
