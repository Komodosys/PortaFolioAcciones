import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { PerfilApi } from '../../core/api/perfil.api';
import { Sesion } from '../../core/sesion/sesion';

/** Un idioma que se puede elegir. */
interface Idioma {
  readonly codigo: string;
  readonly nombre: string;
}

/**
 * Cambia el idioma de la interfaz **sin recargar** (ADR-013).
 *
 * El idioma es una preferencia de la persona, no de la instalacion: la misma aplicacion la usan varias
 * personas de la casa y no tienen por que querer el mismo idioma.
 *
 * El nombre de cada idioma se escribe **en ese idioma** —«Español», «Français», «English»— y no
 * traducido. Es lo que hace un menu de idiomas en cualquier sitio: quien busca el suyo lo reconoce
 * escrito como el lo escribe, no como se dice en el idioma en el que esta la pantalla.
 */
@Component({
  selector: 'app-selector-idioma',
  imports: [TranslocoDirective],
  templateUrl: './selector-idioma.html',
  // El estilo vive en la plantilla, con clases de Tailwind: no queda SCSS que mantener.
  host: { class: 'block' },
})
export class SelectorIdioma {
  private readonly idiomas = inject(TranslocoService);
  private readonly perfil = inject(PerfilApi);
  private readonly sesion = inject(Sesion);

  readonly disponibles: readonly Idioma[] = [
    { codigo: 'es', nombre: 'Español' },
    { codigo: 'fr', nombre: 'Français' },
    { codigo: 'en', nombre: 'English' },
  ];

  /**
   * El idioma activo, siguiendo los cambios.
   *
   * Se sigue y no se guarda en una copia local porque el idioma **tambien cambia desde fuera** de este
   * componente: al entrar, la aplicacion pone el de la persona segun su preferencia. Con una copia
   * local, el selector seguiria enseñando el anterior.
   */
  readonly activo = toSignal(this.idiomas.langChanges$, {
    initialValue: this.idiomas.getActiveLang(),
  });

  /** El nombre del idioma activo, para el boton. */
  readonly nombreActivo = computed(
    () => this.disponibles.find((idioma) => idioma.codigo === this.activo())?.nombre ?? '',
  );

  /**
   * Cambia el idioma **y lo guarda**.
   *
   * El cambio se aplica primero y se guarda despues, y ese orden importa: si se esperara a la respuesta,
   * cambiar de idioma tardaria lo que tarde la red, y si la red falla no cambiaria nada. Asi la interfaz
   * responde al instante, y lo peor que puede pasar si el guardado falla es que al volver a entrar
   * aparezca el idioma anterior.
   *
   * Se guarda aqui y no solo con el boton de la pantalla de preferencias porque **es el mismo ajuste**:
   * quien lo cambia desde la cabecera espera que se quede, no que se pierda al recargar.
   */
  async cambiar(codigo: string): Promise<void> {
    this.idiomas.setActiveLang(codigo);

    const usuario = this.sesion.usuario();

    // Sin sesion no hay nada que guardar: el selector tambien se ve en la pantalla de entrada, y alli la
    // preferencia es de la instalacion, no de nadie.
    if (!usuario) {
      return;
    }

    try {
      this.sesion.actualizarPerfil(
        await this.perfil.guardarPreferencias(usuario.baseCurrency, codigo),
      );
    } catch {
      // Se traga el fallo a proposito: el idioma YA ha cambiado, y avisar de que no se ha podido guardar
      // seria interrumpir por algo que la persona notara como mucho al volver a entrar.
    }
  }
}
