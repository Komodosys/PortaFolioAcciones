import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { Porcion, arcosDe } from '../graficos';

/**
 * Un donut con su leyenda.
 *
 * **El agujero del centro no es decoración**: ahí va el total, que es la cifra que da sentido al dibujo. Sin
 * ella, una tarta obliga a sumar las porciones para saber de qué es el 100 %.
 *
 * **Y lleva leyenda siempre**, aunque el gráfico sea pequeño: el color por sí solo no distingue nada para quien
 * no ve bien los colores, y un gráfico sin leyenda no se puede leer en blanco y negro ni imprimir.
 *
 * No calcula importes: recibe las porciones ya repartidas por el servidor y solo las convierte en arcos.
 */
@Component({
  selector: 'app-donut',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t">
      @if (porciones().length === 0) {
        <p class="text-sm text-slate-500">{{ t('grafico.sinDatos') }}</p>
      } @else {
        <div class="flex flex-col items-center gap-6 lg:flex-row lg:items-center lg:justify-center">
          <svg viewBox="0 0 100 100" class="h-56 w-56 shrink-0" role="img" [attr.aria-label]="etiqueta()">
            <title>{{ etiqueta() }}</title>

            @for (d of arcos(); track $index) {
              <path [attr.d]="d" [attr.fill]="color($index)" class="transition-opacity hover:opacity-80" />
            }

            <!--
              El total, en el agujero. Va en DOS líneas y no en una: «86.881,52 CAD» de una pieza no cabe en el
              círculo y se sale por encima del anillo, que es lo que hacía que la cifra se viera deformada. La
              moneda baja a su propia línea, más pequeña.
            -->
            <text
              x="50"
              y="45"
              text-anchor="middle"
              class="fill-slate-400 text-[6px] font-bold uppercase tracking-wide"
            >
              {{ t('grafico.total') }}
            </text>
            <text x="50" y="56" text-anchor="middle" class="fill-slate-800 text-[8px] font-bold tabular-nums">
              {{ importeSinMoneda() }}
            </text>
            @if (moneda(); as codigo) {
              <text x="50" y="64" text-anchor="middle" class="fill-slate-400 text-[6px] font-semibold">
                {{ codigo }}
              </text>
            }
          </svg>

          <!--
            La leyenda va en dos columnas y con un ancho máximo, no estirada por toda la tarjeta: con la tarjeta
            entera, el nombre quedaba a un extremo y el importe al otro, a más de mil píxeles de distancia, y
            había que recorrer la fila con el dedo para emparejarlos.
          -->
          <ul class="grid w-full max-w-2xl list-none grid-cols-1 gap-x-8 gap-y-2 p-0 sm:grid-cols-2">
            @for (porcion of porciones(); track porcion.etiqueta) {
              <li class="flex items-baseline gap-2 text-sm">
                <span class="h-3 w-3 shrink-0 rounded-sm" [style.background-color]="color($index)"></span>
                <span class="truncate text-slate-600">{{ porcion.etiqueta }}</span>
                <span class="ml-auto shrink-0 font-semibold tabular-nums text-slate-800">{{ importe(porcion.valor) }}</span>
                <span class="w-11 shrink-0 text-right text-xs tabular-nums text-slate-400">{{ porcentaje(porcion.parte) }}</span>
              </li>
            }
          </ul>
        </div>
      }
    </ng-container>
  `,
})
export class Donut {
  /** Las porciones, ya repartidas. */
  readonly porciones = input.required<readonly Porcion[]>();

  /** El total, ya formateado con su moneda. */
  readonly total = input.required<string>();

  /** Descripción del gráfico para quien no lo ve. */
  readonly etiqueta = input.required<string>();

  /** Formato del importe, que pone quien lo usa: el cliente formatea, no calcula. */
  readonly formatear = input.required<(valor: number) => string>();

  readonly arcos = computed(() => arcosDe(this.porciones()));

  // Una paleta fija y sobria. No se generan colores al azar: los mismos datos tienen que tener el mismo color
  // entre pantallas y entre dos visitas, o el gráfico parece otro cada vez que se abre.
  private readonly paleta = [
    '#0f766e',
    '#0ea5e9',
    '#6366f1',
    '#8b5cf6',
    '#d946ef',
    '#f59e0b',
    '#84cc16',
    '#94a3b8',
  ];

  color(indice: number): string {
    return this.paleta[indice % this.paleta.length];
  }

  importe(valor: number): string {
    return this.formatear()(valor);
  }

  /**
   * El total sin la moneda, para el agujero.
   *
   * El importe llega formateado en una sola cadena —«86.881,52 CAD»— y se parte por el último espacio. El código
   * de la moneda va siempre al final porque es como lo escribe el formateador de la casa en los tres idiomas, así
   * que no hay que adivinar el separador de millares de cada idioma.
   */
  readonly importeSinMoneda = computed(() => this.partir(this.total()).numero);

  /** El código de la moneda, o vacío si el importe venía sin ella. */
  readonly moneda = computed(() => this.partir(this.total()).moneda);

  private partir(total: string): { numero: string; moneda: string } {
    const texto = total.trim();
    const corte = texto.lastIndexOf(' ');

    return corte > 0
      ? { numero: texto.slice(0, corte), moneda: texto.slice(corte + 1) }
      : { numero: texto, moneda: '' };
  }

  porcentaje(parte: number): string {
    return `${(parte * 100).toFixed(1)} %`;
  }
}
