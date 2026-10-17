import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { codigoDe } from '../../core/api/interceptores';
import { PerfilApi } from '../../core/api/perfil.api';
import { Sesion } from '../../core/sesion/sesion';

/** Una opcion de un desplegable: una moneda o un idioma. */
interface Opcion {
  readonly codigo: string;
  readonly nombre: string;
}

/**
 * Las preferencias de quien tiene la sesion abierta.
 *
 * Son **dos y van juntas**: la moneda en la que quiere ver sus totales y el idioma de la interfaz. Se
 * guardan de una vez porque son la misma decision —«como quiero ver esto»— y porque dos peticiones
 * dejarian las preferencias a medias si la segunda fallara.
 */
@Component({
  selector: 'app-preferencias',
  imports: [FormsModule, TranslocoDirective],
  templateUrl: './preferencias.html',
  // El estilo vive en la plantilla, con clases de Tailwind: no queda SCSS que mantener.
  host: { class: 'block' },
})
export class Preferencias {
  private readonly api = inject(PerfilApi);

  readonly sesion = inject(Sesion);

  /**
   * Las monedas que se ofrecen.
   *
   * La API acepta **cualquier** codigo de tres letras, asi que esta lista no limita: es un atajo para no
   * tener que teclear el codigo. Se ofrecen las que de verdad se usan aqui —el dolar canadiense y el
   * estadounidense, porque las posiciones son de los dos paises— y las dos grandes por si algun dia
   * entra algo de fuera.
   */
  readonly monedas: readonly Opcion[] = [
    { codigo: 'CAD', nombre: 'Dólar canadiense' },
    { codigo: 'USD', nombre: 'Dólar estadounidense' },
    { codigo: 'EUR', nombre: 'Euro' },
    { codigo: 'GBP', nombre: 'Libra esterlina' },
  ];

  /** La moneda elegida. Arranca con la del perfil. */
  readonly moneda = signal('');

  readonly idioma = signal('');

  readonly guardando = signal(false);
  readonly guardado = signal(false);
  readonly fallo = signal<string | null>(null);

  /** Si la moneda del perfil no esta en la lista, se añade: no se puede enseñar una eleccion que no aparece. */
  readonly monedasOfrecidas = computed(() => {
    const actual = this.sesion.usuario()?.baseCurrency;
    const lista = this.monedas;

    if (actual && !lista.some((moneda) => moneda.codigo === actual)) {
      return [...lista, { codigo: actual, nombre: actual }];
    }

    return lista;
  });

  readonly idiomas: readonly Opcion[] = [
    { codigo: 'es', nombre: 'Español' },
    { codigo: 'fr', nombre: 'Français' },
    { codigo: 'en', nombre: 'English' },
  ];

  constructor() {
    this.moneda.set(this.sesion.usuario()?.baseCurrency ?? 'CAD');
    this.idioma.set(this.sesion.usuario()?.language ?? 'en');
  }

  async guardar(): Promise<void> {
    if (this.guardando()) {
      return;
    }

    this.guardando.set(true);
    this.guardado.set(false);
    this.fallo.set(null);

    try {
      // Se guarda en el servidor y se refresca la sesion con lo que devuelve. No se da por hecho que se
      // guardo lo que se mando: la API es quien manda, y si normaliza algo, lo que vale es lo suyo.
      this.sesion.actualizarPerfil(
        await this.api.guardarPreferencias(this.moneda(), this.idioma()),
      );

      this.guardado.set(true);
    } catch (error: unknown) {
      this.fallo.set(
        error instanceof HttpErrorResponse ? (codigoDe(error) ?? 'comun.error') : 'comun.error',
      );
    } finally {
      this.guardando.set(false);
    }
  }
}
