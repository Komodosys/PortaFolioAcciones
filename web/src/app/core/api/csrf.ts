import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * Guarda el testigo CSRF que la API exige en toda peticion que cambie algo.
 *
 * **Por que se pide y se renueva al entrar.** El testigo esta ligado a QUIEN lo pide, asi que el que se
 * consigue estando sin sesion deja de valer en cuanto se entra. La API devuelve uno nuevo en la propia
 * respuesta del inicio de sesion, y aqui se guarda: si no, la primera accion despues de entrar fallaria
 * con un error que no tiene nada que ver con lo que la persona esta haciendo.
 *
 * Se guarda **en memoria y no en una cookie**: la cookie la pone el servidor y es `HttpOnly` a
 * proposito. Lo que viaja en cabecera es este valor, y tenerlo en memoria hace que se pierda al cerrar
 * la pestana, que es lo que se quiere.
 */
@Injectable({ providedIn: 'root' })
export class Csrf {
  private readonly http = inject(HttpClient);
  private readonly testigo = signal<string | null>(null);

  /** El testigo actual, o nulo si todavia no se ha pedido. */
  readonly actual = this.testigo.asReadonly();

  /**
   * Devuelve el testigo, pidiendolo si hace falta.
   *
   * Dos peticiones a la vez comparten una sola peticion de testigo: sin eso, abrir dos pantallas al
   * mismo tiempo pediria dos y la segunda pisaria a la primera.
   */
  async asegurar(): Promise<string> {
    const guardado = this.testigo();

    if (guardado) {
      return guardado;
    }

    if (!this.pendiente) {
      this.pendiente = firstValueFrom(this.http.get<{ token: string }>('/api/auth/csrf'))
        .then((respuesta) => {
          this.testigo.set(respuesta.token);
          this.pendiente = null;
          return respuesta.token;
        })
        .catch((error: unknown) => {
          this.pendiente = null;
          throw error;
        });
    }

    return this.pendiente;
  }

  /**
   * Guarda el testigo que devuelve la API al entrar o al salir.
   *
   * Se acepta `null` a proposito: al cerrar sesion hay que **olvidarlo**, porque pertenecia a la sesion
   * que acaba de terminar.
   */
  guardar(testigo: string | null): void {
    this.testigo.set(testigo);
  }

  private pendiente: Promise<string> | null = null;
}
