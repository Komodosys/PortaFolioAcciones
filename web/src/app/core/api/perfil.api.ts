import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { UsuarioSesion } from '../sesion/modelos';

/**
 * Las preferencias de quien tiene la sesion abierta.
 *
 * **Ninguna direccion lleva el identificador de la persona**: quien es se sabe por la sesion, en el
 * servidor.
 */
@Injectable({ providedIn: 'root' })
export class PerfilApi {
  private readonly http = inject(HttpClient);

  /** El perfil. */
  obtener(): Promise<UsuarioSesion> {
    return firstValueFrom(this.http.get<UsuarioSesion>('/api/profile'));
  }

  /**
   * Cambia la moneda base y el idioma.
   *
   * Los dos van juntos y no en dos peticiones: son la misma pantalla. Con dos, un fallo en la segunda
   * dejaria las preferencias a medias sin que nadie lo notara.
   */
  guardarPreferencias(monedaBase: string, idioma: string): Promise<UsuarioSesion> {
    return firstValueFrom(
      this.http.put<UsuarioSesion>('/api/profile/preferences', {
        baseCurrency: monedaBase,
        language: idioma,
      }),
    );
  }
}
