import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { ResumenDeCartera } from '../modelos';

/** Cómo se pinta la tarjeta. Las tres secciones de la lista no son iguales, y no es decoración. */
export type AspectoDeTarjeta = 'mio' | 'archivada' | 'ajena';

/**
 * Una tarjeta de cartera con su menú de tres acciones: editar, archivar y borrar.
 *
 * Es un componente y no un trozo de plantilla repetido porque hay tres secciones que enseñan carteras —las
 * mías, las archivadas y las de otras personas— y el menú tiene que estar en las tres.
 *
 * **No sabe nada de la API.** Solo dice «quieren editar esta» y «quieren borrar esta»; quién hace qué es
 * cosa de la pantalla que la usa. Así el componente no tiene que saber si quien mira es administrador ni
 * si la cartera es suya.
 */
@Component({
  selector: 'app-tarjeta-de-cartera',
  imports: [RouterLink, TranslocoDirective],
  templateUrl: './tarjeta-de-cartera.html',
  host: { class: 'contents' },
})
export class TarjetaDeCartera {
  readonly cartera = input.required<ResumenDeCartera>();

  /** `mio`, `archivada` o `ajena`. Cambia el borde y el color, que es lo que evita tocar datos ajenos sin notarlo. */
  readonly aspecto = input<AspectoDeTarjeta>('mio');

  /** Si hay una acción en marcha sobre esta tarjeta, para no pulsar dos veces. */
  readonly ocupada = input(false);

  readonly editar = output<ResumenDeCartera>();
  readonly archivar = output<ResumenDeCartera>();
  readonly borrar = output<ResumenDeCartera>();
}
