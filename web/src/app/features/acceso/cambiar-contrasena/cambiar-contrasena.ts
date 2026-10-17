import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Sesion } from '../../../core/sesion/sesion';

/**
 * Cambiar la contrasena propia.
 *
 * Aparece obligatoriamente cuando la contrasena es **temporal**: el administrador la genero al
 * restablecerla y se entrego en mano, asi que hay que cambiarla o el administrador seguiria conociendola.
 *
 * Al cambiarla, la API **invalida la sesion** a proposito: si alguien mas estaba dentro con la
 * contrasena antigua, deja de estarlo. Por eso despues se vuelve a la pantalla de entrada.
 */
@Component({
  selector: 'app-cambiar-contrasena',
  imports: [FormsModule, TranslocoDirective],
  templateUrl: './cambiar-contrasena.html',
  styleUrl: './cambiar-contrasena.scss',
})
export class CambiarContrasena {
  private readonly sesion = inject(Sesion);
  private readonly router = inject(Router);

  actual = '';
  nueva = '';
  repetir = '';

  readonly enviando = signal(false);
  readonly fallo = signal<string | null>(null);

  /** La clave de traduccion del aviso, o nula. Se compone al vuelo para que siga al idioma. */
  readonly claveDeError = computed(() => {
    const codigo = this.fallo();
    return codigo ? `acceso.errores.${codigo}` : null;
  });

  readonly esTemporal = this.sesion.debeCambiarContrasena;

  async enviar(): Promise<void> {
    if (this.enviando()) {
      return;
    }

    this.fallo.set(null);

    if (!this.actual || !this.nueva) {
      this.fallo.set('camposFaltantes');
      return;
    }

    // Se comprueba aqui para no gastar una peticion. La API valida la nueva contra la politica de
    // verdad; esto solo evita el viaje cuando esta claro que no van a coincidir.
    if (this.nueva !== this.repetir) {
      this.fallo.set('noCoinciden');
      return;
    }

    this.enviando.set(true);

    try {
      const error = await this.sesion.cambiarContrasena(this.actual, this.nueva);

      if (error) {
        this.fallo.set(error);
        return;
      }

      this.sesion.olvidarCambioPendiente();
      await this.router.navigate(['/entrar']);
    } finally {
      this.enviando.set(false);
    }
  }
}
