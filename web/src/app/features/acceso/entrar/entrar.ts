import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Sesion } from '../../../core/sesion/sesion';
import { ResultadoEntrada } from '../../../core/sesion/modelos';
import { SelectorIdioma } from '../../../shared/selector-idioma/selector-idioma';

/**
 * La pantalla de entrada.
 *
 * **No hay registro**: esta aplicacion es de una casa, no de usuarios por captar. Las cuentas las crea el
 * administrador, y por eso aqui solo hay un formulario de entrada.
 */
@Component({
  selector: 'app-entrar',
  imports: [FormsModule, TranslocoDirective, SelectorIdioma],
  templateUrl: './entrar.html',
  // El estilo vive en la plantilla, con clases de Tailwind: no queda SCSS que mantener.
  host: { class: 'block' },
})
export class Entrar {
  private readonly sesion = inject(Sesion);
  private readonly router = inject(Router);

  usuario = '';
  contrasena = '';

  /** Si la peticion esta en marcha: sirve para desactivar el boton y no mandar dos veces. */
  readonly enviando = signal(false);

  /** El codigo del ultimo fallo, o nulo si no ha habido ninguno. */
  readonly fallo = signal<ResultadoEntrada | null>(null);

  /**
   * La clave de traduccion del error, o nula.
   *
   * Se compone aqui y no se guarda el texto: el idioma puede cambiar mientras el aviso esta en pantalla,
   * y el mensaje debe cambiar con el.
   */
  readonly claveDeError = computed(() => {
    const codigo = this.fallo();

    if (!codigo || codigo === 'ok') {
      return null;
    }

    return `acceso.errores.${codigo}`;
  });

  async enviar(): Promise<void> {
    if (this.enviando()) {
      return;
    }

    this.fallo.set(null);

    // Se comprueba antes de salir para no gastar una peticion que la API va a rechazar. La API lo
    // comprueba tambien: esto es comodidad, no validacion.
    if (!this.usuario.trim() || !this.contrasena) {
      this.fallo.set('camposFaltantes');
      return;
    }

    this.enviando.set(true);

    try {
      const resultado = await this.sesion.entrar(this.usuario, this.contrasena);

      if (resultado !== 'ok') {
        this.fallo.set(resultado);
        return;
      }

      // Con contrasena temporal, primero se cambia. Dejar entrar y pedirlo despues haria que la persona
      // se encontrara con la peticion a mitad de otra cosa.
      const destino = this.sesion.debeCambiarContrasena() ? '/cambiar-contrasena' : '/carteras';
      await this.router.navigate([destino]);
    } finally {
      this.enviando.set(false);
    }
  }
}
