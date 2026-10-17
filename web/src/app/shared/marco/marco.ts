import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Sesion } from '../../core/sesion/sesion';
import { SelectorIdioma } from '../selector-idioma/selector-idioma';

/**
 * El marco de la aplicacion: la cabecera y el hueco donde se pinta cada pantalla.
 *
 * Todo lo que no sea la pantalla de entrada vive aqui dentro, y asi la cabecera **no se repite en cada
 * pantalla ni se olvida en ninguna**. Añadir una pantalla nueva no obliga a acordarse de nada.
 */
@Component({
  selector: 'app-marco',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoDirective, SelectorIdioma],
  templateUrl: './marco.html',
  styleUrl: './marco.scss',
})
export class Marco {
  private readonly sesion = inject(Sesion);
  private readonly router = inject(Router);

  /** Quien tiene la sesion abierta. */
  readonly usuario = this.sesion.usuario;

  /** Si es administrador: es lo que decide si se enseña la pantalla de administracion. */
  readonly esAdministrador = this.sesion.esAdministrador;

  /**
   * Si el menu esta desplegado en pantalla estrecha.
   *
   * En el movil la cabecera no cabe, asi que las opciones se esconden detras de un boton. Se guarda en
   * una señal para que la plantilla reaccione sola.
   */
  readonly menuAbierto = signal(false);

  alternarMenu(): void {
    this.menuAbierto.update((abierto) => !abierto);
  }

  cerrarMenu(): void {
    this.menuAbierto.set(false);
  }

  async salir(): Promise<void> {
    this.cerrarMenu();
    await this.sesion.salir();

    // Se navega DESPUES de cerrar: al reves, la guarda de la ruta actual veria la sesion todavia abierta.
    await this.router.navigate(['/entrar']);
  }
}
