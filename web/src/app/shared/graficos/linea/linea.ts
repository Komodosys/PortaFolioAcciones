import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { dibujarSerie } from '../graficos';

/** Un punto de la evolución, tal como lo pinta la pantalla. */
export interface PuntoDeEvolucion {
  readonly fecha: string;

  /** Lo que valía la cartera ese día. */
  readonly valor: number;

  /**
   * Lo que se había puesto hasta ese día. **Nulo** cuando no se sabe —una cartera que se dio de alta como una
   * foto— y entonces no se dibuja la línea: dibujarla a cero diría que no se puso nada.
   */
  readonly aportado: number | null;
}

/**
 * La evolución de una cartera: el valor y lo puesto, en el tiempo.
 *
 * **Se dibujan dos líneas y no una.** «Cuánto tengo» sin «cuánto puse» no dice si va bien: una cartera que sube
 * porque se aportó más dinero no ha ganado nada. La distancia entre las dos es lo ganado.
 *
 * **Las dos comparten escala**, y no es un detalle: con escalas distintas se cruzarían donde no se cruzaron y el
 * gráfico diría justo lo contrario de lo que pasó.
 */
@Component({
  selector: 'app-linea',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t">
      @if (puntos().length === 0) {
        <p class="text-sm text-slate-500">{{ t('grafico.sinDatos') }}</p>
      } @else {
        <svg viewBox="0 0 100 60" preserveAspectRatio="none" class="h-48 w-full" role="img" [attr.aria-label]="etiqueta()">
          <title>{{ etiqueta() }}</title>

          <!-- La rejilla: tres líneas finas. Dan referencia sin tapar el trazo. -->
          @for (y of [6, 30, 54]; track y) {
            <line x1="0" [attr.y1]="y" x2="100" [attr.y2]="y" stroke="#e2e8f0" stroke-width="0.3" />
          }

          <!-- Lo aportado va debajo y punteado: es la referencia, no el protagonista. -->
          @if (dibujo().aportado; as aportado) {
            <path
              [attr.d]="aportado.trazo"
              fill="none"
              stroke="#94a3b8"
              stroke-width="0.7"
              stroke-dasharray="2 1.5"
              vector-effect="non-scaling-stroke"
            />
          }

          <path
            [attr.d]="dibujo().valor.trazo"
            fill="none"
            stroke="#0f766e"
            stroke-width="1"
            stroke-linejoin="round"
            vector-effect="non-scaling-stroke"
          />
        </svg>

        <div class="mt-2 flex flex-wrap items-center gap-4 text-xs text-slate-500">
          <span class="flex items-center gap-1.5">
            <span class="h-2 w-4 rounded-sm bg-teal-700"></span>
            {{ t('grafico.valor') }}
          </span>
          @if (dibujo().aportado) {
            <span class="flex items-center gap-1.5">
              <span class="h-0.5 w-4 border-t-2 border-dashed border-slate-400"></span>
              {{ t('grafico.aportado') }}
            </span>
          }
          <span class="ml-auto">{{ t('grafico.desdeHasta', { desde: puntos()[0].fecha, hasta: puntos()[puntos().length - 1].fecha }) }}</span>
        </div>
      }
    </ng-container>
  `,
})
export class Linea {
  readonly puntos = input.required<readonly PuntoDeEvolucion[]>();

  /** Descripción del gráfico para quien no lo ve. */
  readonly etiqueta = input.required<string>();

  readonly dibujo = computed(() => {
    const puntos = this.puntos();

    if (puntos.length === 0) {
      return { valor: { puntos: [], trazo: '' }, aportado: null };
    }

    // La escala es de las DOS series juntas: una sola escala por gráfico.
    const valores = puntos.map((punto) => punto.valor);
    const aportados = puntos.map((punto) => punto.aportado).filter((valor): valor is number => valor !== null);

    const todos = [...valores, ...aportados];
    const minimo = Math.min(...todos);
    const maximo = Math.max(...todos);

    return {
      valor: dibujarSerie(valores, minimo, maximo, 100, 60, 6),

      // La línea de lo puesto solo si se sabe en TODOS los puntos. Con un hueco, el trazo mentiría en ese
      // tramo: uniría dos puntos que no son comparables.
      aportado:
        aportados.length === puntos.length
          ? dibujarSerie(puntos.map((punto) => punto.aportado as number), minimo, maximo, 100, 60, 6)
          : null,
    };
  });
}
