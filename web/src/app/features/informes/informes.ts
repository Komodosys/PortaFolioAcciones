import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { codigoDe } from '../../core/api/interceptores';
import { FilaDeRendimiento, InformeDeRendimiento, InformesApi } from '../../core/api/informes.api';

/**
 * El informe de rendimiento: coste contra valor, por cartera.
 *
 * Los filtros son **en memoria** y no llamadas al servidor: el informe ya trae todas las filas que la persona
 * puede ver —el aislamiento lo decide el servidor— así que filtrar es elegir cuáles se enseñan. Pedir el
 * filtrado al servidor sería un viaje por cada cambio de desplegable.
 */
@Component({
  selector: 'app-informes',
  imports: [FormsModule, TranslocoDirective],
  templateUrl: './informes.html',
  host: { class: 'block' },
})
export class Informes {
  private readonly api = inject(InformesApi);

  readonly informe = signal<InformeDeRendimiento | null>(null);
  readonly cargando = signal(true);
  readonly fallo = signal<string | null>(null);

  /** Los filtros. Vacío significa «todas». */
  persona = '';
  tipo = '';

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.fallo.set(null);

    try {
      this.informe.set(await this.api.rendimiento());
    } catch (error: unknown) {
      this.informe.set(null);
      this.fallo.set(
        error instanceof HttpErrorResponse ? (codigoDe(error) ?? 'comun.error') : 'comun.error',
      );
    } finally {
      this.cargando.set(false);
    }
  }

  /** Las personas que aparecen, para el desplegable. */
  readonly personas = computed(() => {
    const nombres = (this.informe()?.rows ?? []).map((fila) => fila.ownerDisplayName);
    return [...new Set(nombres)].sort((a, b) => a.localeCompare(b));
  });

  /** Los tipos que aparecen. */
  readonly tipos = computed(() => {
    const valores = (this.informe()?.rows ?? []).map((fila) => fila.registrationType);
    return [...new Set(valores)].sort((a, b) => a.localeCompare(b));
  });

  readonly filas = computed(() =>
    (this.informe()?.rows ?? []).filter(
      (fila) =>
        (this.persona === '' || fila.ownerDisplayName === this.persona) &&
        (this.tipo === '' || fila.registrationType === this.tipo),
    ),
  );

  /** Los totales de lo que se está viendo, sumados aquí porque el servidor solo da los de todo. */
  readonly totales = computed(() => {
    const filas = this.filas();

    if (filas.length === 0) {
      return null;
    }

    const suma = (campo: keyof FilaDeRendimiento): number =>
      filas.reduce((total, fila) => total + Number(fila[campo] ?? 0), 0);

    // La ganancia realizada solo se suma si TODAS la tienen: si a una le falta, sumar las demás se leería como
    // «lo ganado» y le faltaría una parte. Es la misma regla que aplica el servidor.
    const realizado = filas.every((fila) => fila.realizedGain === null)
      ? null
      : filas.some((fila) => fila.realizedGain === null)
        ? null
        : suma('realizedGain');

    const valor = suma('marketValue');
    const efectivo = suma('cashValue');

    return {
      valor,
      efectivo,
      total: valor + efectivo,
      coste: suma('costBasis'),
      noRealizada: suma('unrealizedGain'),
      aportado: suma('contributed'),
      retirado: suma('withdrawn'),

      // Lo ganado de verdad: el total menos lo aportado. El efectivo ya incorpora lo cobrado en las ventas,
      // asi que la ganancia realizada no se suma aparte: se contaria dos veces.
      // Sin apertura declarada, el total menos lo aportado no es la ganancia: saldria enorme y falsa. Se dice
      // que no se sabe en vez de dar un numero.
      ganado: filas.some((fila) => !fila.hasOpening)
        ? null
        : valor + efectivo - (suma('contributed') - suma('withdrawn')),
      realizado,
    };
  });

  /** Un importe como número, para poder sumarlo y formatearlo. */
  numero(valor: string | number | null): number {
    return valor === null ? 0 : Number(valor);
  }

  limpiarFiltros(): void {
    this.persona = '';
    this.tipo = '';
  }
}
