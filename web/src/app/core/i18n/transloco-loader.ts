import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Translation, TranslocoLoader } from '@jsverse/transloco';

/**
 * Carga las traducciones de un idioma.
 *
 * Se usa una libreria de tiempo de ejecucion (Transloco) y no la internacionalizacion integrada de
 * Angular, porque la integrada es de COMPILACION: genera una compilacion por idioma y no permite
 * cambiarlo desde las preferencias del usuario (ver ADR-013).
 *
 * Los ficheros se sirven desde `public/i18n/`, que en Angular se publica en la raiz del sitio.
 */
@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  constructor(private readonly http: HttpClient) {}

  getTranslation(lang: string) {
    return this.http.get<Translation>(`/i18n/${lang}.json`);
  }
}
