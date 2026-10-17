import { Component, input } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';

/**
 * El aviso de que se estan tocando datos de otra persona.
 *
 * Es **un componente compartido** y no un trozo de plantilla repetido, y no es por ahorrar lineas: este
 * aviso tiene que decir **exactamente lo mismo** en todas las pantallas donde aparece. Copiado, el dia que
 * se matice en una se quedara distinto en las demas, y un aviso que dice cosas distintas segun donde se
 * mire deja de ser un aviso.
 *
 * Va **siempre visible y no se puede cerrar**. Un aviso que se puede descartar es un aviso que desaparece
 * justo cuando se lleva un rato trabajando, que es cuando mas falta hace.
 */
@Component({
  selector: 'app-aviso-datos-ajenos',
  imports: [TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      <div class="aviso" role="status">
        <strong class="aviso__titulo">{{ t('aviso.datosAjenos') }}</strong>
        <span class="aviso__texto">{{ t('aviso.datosAjenosDetalle', { persona: propietario() }) }}</span>
        @if (cuenta(); as nombreDeLaCuenta) {
          <span class="aviso__texto">{{ t('aviso.datosAjenosCuenta', { cuenta: nombreDeLaCuenta }) }}</span>
        }
        <span class="aviso__texto">{{ t('aviso.datosAjenosAuditoria') }}</span>
      </div>
    </ng-container>
  `,
  styles: `
    :host {
      display: block;
      margin-bottom: 1rem;
    }

    /*
     * Ambar y no rojo: no es un error, es una advertencia. En rojo pareceria que algo se ha roto, y lo
     * que pasa es que hay que ir con cuidado.
     */
    .aviso {
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
      padding: 0.75rem 0.85rem;
      border: 1px solid #b8860b;
      border-left-width: 4px;
      border-radius: 0.45rem;
      background: color-mix(in srgb, #b8860b 14%, transparent);
      color: #8a6508;
      font-size: 0.88rem;
    }

    .aviso__titulo {
      font-size: 0.95rem;
    }

    .aviso__texto {
      line-height: 1.4;
    }
  `,
})
export class AvisoDatosAjenos {
  /** Como se llama la persona de la que son los datos. */
  readonly propietario = input.required<string>();

  /**
   * En que cuenta se esta trabajando.
   *
   * Es opcional porque en la pantalla de la cartera el nombre ya esta a la vista, y repetirlo tres
   * centimetros mas arriba solo mete ruido. Se pone donde de verdad hace falta: en una posicion, que es
   * una pantalla a la que se puede llegar sin haber pasado por la cartera.
   */
  readonly cuenta = input<string | null>(null);
}
