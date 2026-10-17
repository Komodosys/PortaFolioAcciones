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
 *
 * El estilo vive en la plantilla, como en el resto de la aplicacion. Es ambar y con borde discontinuo, el
 * mismo recurso que las carteras ajenas del listado: no es un error —en rojo pareceria que algo se ha
 * roto—, es que hay que ir con cuidado.
 */
@Component({
  selector: 'app-aviso-datos-ajenos',
  imports: [TranslocoDirective],
  host: { class: 'mb-4 block' },
  template: `
    <ng-container *transloco="let t">
      <div
        class="rounded-3xl border-2 border-dashed border-amber-300 bg-amber-50/70 p-4 text-amber-900"
        role="status"
      >
        <strong class="block text-base font-bold">{{ t('aviso.datosAjenos') }}</strong>
        <span class="mt-1 block text-sm text-amber-800">
          {{ t('aviso.datosAjenosDetalle', { persona: propietario() }) }}
        </span>
        @if (cuenta(); as nombreDeLaCuenta) {
          <span class="mt-1 block text-sm text-amber-800">
            {{ t('aviso.datosAjenosCuenta', { cuenta: nombreDeLaCuenta }) }}
          </span>
        }
        <span class="mt-1 block text-sm text-amber-800">{{ t('aviso.datosAjenosAuditoria') }}</span>
      </div>
    </ng-container>
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
