import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { Csrf } from '../api/csrf';
import { codigoDe } from '../api/interceptores';
import { RespuestaEntrada, ResultadoEntrada, UsuarioSesion } from './modelos';

/**
 * La sesion de la persona: quien es, como entra y como sale.
 *
 * Es la unica pieza que sabe si hay sesion. Las pantallas preguntan aqui en vez de mirar cookies, que
 * ademas seria imposible: la cookie de sesion es `HttpOnly` a proposito, para que un script inyectado no
 * pueda leerla.
 */
@Injectable({ providedIn: 'root' })
export class Sesion {
  private readonly http = inject(HttpClient);
  private readonly csrf = inject(Csrf);
  private readonly idiomas = inject(TranslocoService);

  private readonly usuarioInterno = signal<UsuarioSesion | null>(null);

  /** La persona que tiene la sesion abierta, o nulo si no hay ninguna. */
  readonly usuario = this.usuarioInterno.asReadonly();

  /** Dice si hay sesion, sin tener que comprobar nulo en cada plantilla. */
  readonly haySesion = computed(() => this.usuarioInterno() !== null);

  /** Dice si quien tiene la sesion es administrador. */
  readonly esAdministrador = computed(() => this.usuarioInterno()?.isAdmin ?? false);

  /** Si hay que cambiar la contrasena antes de dejar hacer nada mas. */
  private readonly cambioPendiente = signal(false);

  readonly debeCambiarContrasena = this.cambioPendiente.asReadonly();

  /**
   * Intenta entrar.
   *
   * Devuelve un **codigo**, no un mensaje: quien llama decide que texto enseñar y en que idioma.
   */
  async entrar(usuario: string, contrasena: string): Promise<ResultadoEntrada> {
    try {
      const respuesta = await firstValueFrom(
        this.http.post<RespuestaEntrada>('/api/auth/login', {
          userName: usuario,
          password: contrasena,
        }),
      );

      // El testigo se guarda ANTES de pedir el perfil: el que habia pertenecia a la sesion anonima y ya
      // no vale. Si se guardara despues, la peticion del perfil fallaria por testigo invalido.
      this.csrf.guardar(respuesta.csrfToken);
      this.cambioPendiente.set(respuesta.mustChangePassword);

      await this.cargarPerfil();

      return 'ok';
    } catch (error: unknown) {
      return this.traducirErrorDeEntrada(error);
    }
  }

  /** Cierra la sesion y olvida todo lo de la persona. */
  async salir(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', {}));
    } catch {
      // Aunque la API falle, la sesion local se cierra igual: dejar a alguien «dentro» porque el servidor
      // no respondio seria lo peor de los dos mundos.
    } finally {
      this.olvidar();
    }
  }

  /**
   * Recupera la sesion si la cookie sigue viva.
   *
   * Se llama una sola vez, al arrancar o al entrar en la primera ruta protegida. Sin esto, recargar la
   * pagina echara a la persona a la pantalla de entrada aunque su sesion siguiera abierta.
   */
  async restaurar(): Promise<void> {
    if (this.intentado) {
      return;
    }

    this.intentado = true;

    try {
      await this.cargarPerfil();
    } catch {
      this.usuarioInterno.set(null);
    }
  }

  /**
   * Cambia la contrasena propia.
   *
   * Devuelve el codigo del problema, o nulo si salio bien. Al cambiarla, la API invalida la sesion, asi
   * que aqui tambien se olvida: la persona tendra que entrar con la nueva, que es justo lo que se busca.
   */
  async cambiarContrasena(actual: string, nueva: string): Promise<string | null> {
    try {
      await firstValueFrom(
        this.http.post('/api/auth/change-password', {
          currentPassword: actual,
          newPassword: nueva,
        }),
      );

      this.olvidar();
      return null;
    } catch (error: unknown) {
      return error instanceof HttpErrorResponse ? codigoDe(error) : 'error';
    }
  }

  /** Marca que ya no hay cambio pendiente, tras cambiarla. */
  olvidarCambioPendiente(): void {
    this.cambioPendiente.set(false);
  }

  /**
   * Refresca el perfil que guarda la sesion.
   *
   * Se llama despues de cambiar las preferencias. Sin esto, la sesion seguiria diciendo la moneda y el
   * idioma viejos, y las pantallas los enseñarian mal **hasta recargar** — que es el peor momento para
   * enterarse de que el cambio no cuajo.
   */
  actualizarPerfil(perfil: UsuarioSesion): void {
    this.usuarioInterno.set(perfil);
    this.aplicarIdioma(perfil.language);
  }

  private async cargarPerfil(): Promise<void> {
    const perfil = await firstValueFrom(this.http.get<UsuarioSesion>('/api/auth/me'));
    this.actualizarPerfil(perfil);
  }

  private aplicarIdioma(codigo: string): void {
    // Solo se cambia si de verdad es otro: `setActiveLang` dispara una recarga de las traducciones, y
    // llamarlo con el idioma que ya esta puesto seria pedirlas otra vez para nada.
    if (codigo && codigo !== this.idiomas.getActiveLang()) {
      this.idiomas.setActiveLang(codigo);
    }
  }

  private olvidar(): void {
    this.usuarioInterno.set(null);
    this.cambioPendiente.set(false);
    this.csrf.guardar(null);
  }

  private traducirErrorDeEntrada(error: unknown): ResultadoEntrada {
    if (!(error instanceof HttpErrorResponse)) {
      return 'error';
    }

    switch (codigoDe(error)) {
      case 'auth.invalid_credentials':
        return 'credenciales';

      case 'auth.locked_out':
        return 'bloqueado';

      case 'auth.temporary_password_expired':
        return 'contrasenaCaducada';

      case 'auth.missing_fields':
        return 'camposFaltantes';

      default:
        return 'error';
    }
  }

  private intentado = false;
}
