import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { codigoDe } from '../../../core/api/interceptores';
import { Idioma } from '../../../core/i18n/idioma';
import { Sesion } from '../../../core/sesion/sesion';
import { AvisoDatosAjenos } from '../../../shared/aviso-datos-ajenos/aviso-datos-ajenos';
import {
  formatearCantidad,
  formatearFecha,
  formatearImporte,
} from '../../../shared/formato/numeros';
import {
  CuentaDeLaPosicion,
  Posicion,
  PosicionesApi,
} from '../../../core/api/posiciones/posiciones.api';

@Component({
  selector: 'app-detalle-de-posicion',
  imports: [FormsModule, TranslocoDirective, AvisoDatosAjenos],
  templateUrl: './detalle.html',
  styleUrl: './detalle.scss',
})
export class DetalleDePosicion {
  private readonly api = inject(PosicionesApi);
  private readonly router = inject(Router);
  private readonly idioma = inject(Idioma);
  private readonly sesion = inject(Sesion);

  readonly id = input.required<string>();

  readonly posicion = signal<Posicion | null>(null);

  /** La cuenta en la que esta: como se llama y de quien es. */
  readonly cuenta = signal<CuentaDeLaPosicion | null>(null);

  /** Dice si la posicion es de otra persona. */
  readonly esAjena = computed(() => {
    const cuenta = this.cuenta();
    const yo = this.sesion.usuario()?.id;

    // Sin sesion no se deberia llegar aqui; si se llega, se dice que es ajena. Ante la duda, avisar.
    return cuenta !== null && (yo === undefined || cuenta.ownerUserId !== yo);
  });
  readonly cargando = signal(true);
  readonly fallo = signal<string | null>(null);
  readonly operando = signal(false);

  // Formularios
  nuevaCantidad = '';
  nuevoCoste = '';
  nuevaFecha = '';
  nuevaExactitud = 'Exact';

  cantidadAVender = '';

  precioImporte = '';
  precioMoneda = '';
  precioFecha = '';

  notas = '';

  constructor() {
    effect(() => {
      void this.cargar(this.id());
    });
  }

  async cargar(id: string): Promise<void> {
    this.cargando.set(true);
    this.fallo.set(null);

    try {
      const posicion = await this.api.obtener(id);
      this.posicion.set(posicion);
      this.notas = posicion.notes ?? '';
      this.precioMoneda = posicion.manualPriceCurrency ?? posicion.costCurrency;
      this.precioFecha = posicion.manualPriceDate ?? '';
      this.precioImporte = posicion.manualPriceAmount?.toString() ?? '';

      // La cuenta se pide aparte, y si falla NO se impide ver la posicion: es mejor una pantalla sin el
      // aviso de quien es que una pantalla que no carga.
      try {
        this.cuenta.set(await this.api.cuentaDe(id));
      } catch {
        this.cuenta.set(null);
      }
    } catch {
      this.posicion.set(null);
      this.fallo.set('posiciones.noEncontrada');
    } finally {
      this.cargando.set(false);
    }
  }

  readonly cantidad = computed(() =>
    this.posicion() ? formatearCantidad(this.posicion()!.quantity, this.idioma.activo()) : '',
  );

  readonly costeMedio = computed(() =>
    this.posicion()
      ? formatearImporte(
          this.posicion()!.averageCostPerUnit,
          this.posicion()!.costCurrency,
          this.idioma.activo(),
        )
      : '',
  );

  readonly costeTotal = computed(() =>
    this.posicion()
      ? formatearImporte(
          this.posicion()!.totalCost,
          this.posicion()!.costCurrency,
          this.idioma.activo(),
        )
      : '',
  );

  readonly lotes = computed(() => {
    const posicion = this.posicion();

    if (!posicion) {
      return [];
    }

    const idioma = this.idioma.activo();

    return posicion.lots.map((lote) => ({
      id: lote.id,
      cantidad: formatearCantidad(lote.quantity, idioma),
      coste: formatearImporte(lote.costPerUnit, posicion.costCurrency, idioma),
      total: formatearImporte(lote.quantity * lote.costPerUnit, posicion.costCurrency, idioma),
      fecha: formatearFecha(lote.purchaseDate, idioma),
      aproximada: lote.dateAccuracy !== 'Exact',
    }));
  });

  async anadirCompra(): Promise<void> {
    await this.ejecutar(async () => {
      const cantidad = numero(this.nuevaCantidad);
      const coste = numero(this.nuevoCoste);

      if (!(cantidad > 0) || !(coste >= 0) || !this.nuevaFecha) {
        this.fallo.set('posiciones.compraMalFormada');
        return;
      }

      this.posicion.set(
        await this.api.anadirCompra(this.id(), {
          quantity: cantidad,
          costPerUnit: coste,
          purchaseDate: this.nuevaFecha,
          dateAccuracy: this.nuevaExactitud,
        }),
      );

      this.nuevaCantidad = '';
      this.nuevoCoste = '';
      this.nuevaFecha = '';
    });
  }

  async vender(): Promise<void> {
    await this.ejecutar(async () => {
      const cantidad = numero(this.cantidadAVender);

      if (!(cantidad > 0)) {
        this.fallo.set('posiciones.cantidadInvalida');
        return;
      }

      this.posicion.set(await this.api.vender(this.id(), cantidad));
      this.cantidadAVender = '';
    });
  }

  async ponerPrecio(): Promise<void> {
    await this.ejecutar(async () => {
      const importe = numero(this.precioImporte);

      if (!(importe > 0) || !this.precioFecha) {
        this.fallo.set('posiciones.precioInvalido');
        return;
      }

      this.posicion.set(
        await this.api.ponerPrecioManual(
          this.id(),
          importe,
          this.precioMoneda,
          this.precioFecha,
        ),
      );
    });
  }

  async alternarCierre(): Promise<void> {
    await this.ejecutar(async () => {
      const posicion = this.posicion()!;
      this.posicion.set(
        posicion.isClosed ? await this.api.reabrir(this.id()) : await this.api.cerrar(this.id()),
      );
    });
  }

  async guardarNotas(): Promise<void> {
    await this.ejecutar(async () => {
      this.posicion.set(await this.api.ponerNotas(this.id(), this.notas.trim() || null));
    });
  }

  volver(): void {
    const posicion = this.posicion();

    if (posicion) {
      void this.router.navigate(['/carteras', posicion.portfolioId]);
    }
  }

  private async ejecutar(accion: () => Promise<void>): Promise<void> {
    if (this.operando()) {
      return;
    }

    this.operando.set(true);
    this.fallo.set(null);

    try {
      await accion();
    } catch (error: unknown) {
      this.fallo.set(
        error instanceof HttpErrorResponse ? (codigoDe(error) ?? 'comun.error') : 'comun.error',
      );
    } finally {
      this.operando.set(false);
    }
  }
}

/** Acepta la coma y el punto: quien escribe en espanol teclea `165,54` y quien escribe en ingles `165.54`. */
function numero(texto: string): number {
  const limpio = texto.trim().replace(',', '.');
  return limpio ? Number(limpio) : Number.NaN;
}
