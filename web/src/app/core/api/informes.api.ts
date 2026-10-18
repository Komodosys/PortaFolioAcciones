import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** El rendimiento de una cartera. */
export interface FilaDeRendimiento {
  readonly portfolioId: string;
  readonly name: string;
  readonly ownerDisplayName: string;

  /** `RegisteredRetirement`, `RegisteredSavings` o `NotRegistered`. */
  readonly registrationType: string;

  readonly currency: string;
  readonly marketValue: string | number;

  /** El dinero que hay en la cuenta. */
  readonly cashValue: string | number;
  readonly costBasis: string | number;
  readonly unrealizedGain: string | number;

  /**
   * Lo que ha entrado desde fuera, **en la moneda de la fila**, o nulo si no se pudo sumar entero.
   *
   * Nulo y no una suma parcial: si algún movimiento está en otra moneda y no tiene tipo de cambio guardado, la
   * suma a la que le falta una parte se leería como el total. Es la misma regla que la ganancia realizada.
   */
  readonly contributed: string | number | null;

  /** Lo que ha salido hacia fuera, en la moneda de la fila, o nulo por el mismo motivo. */
  readonly withdrawn: string | number | null;

  /**
   * Lo que ha entrado por traspaso desde otra cuenta del titular, en la moneda de la fila.
   *
   * Va aparte de `contributed` a propósito: **un traspaso no es una aportación** —el dinero ya era suyo— y
   * tampoco es ganancia. Se resta en «lo ganado», o esa cifra diría que se ganó todo lo que se movió de sitio.
   */
  readonly transfersIn: string | number | null;

  /** Lo que ha salido hacia otra cuenta del titular. */
  readonly transfersOut: string | number | null;

  /**
   * Lo ganado en lo vendido, o **nulo** si alguna venta no tiene el coste guardado.
   *
   * Nulo y no cero: cero diría «no se ganó nada», que es una afirmación; nulo dice «no se sabe», que es la
   * verdad sobre las ventas registradas antes de que ese dato existiera.
   */
  readonly realizedGain: string | number | null;

  /** El día de las cifras de valor y coste, o nulo si esa cartera aún no tiene foto. */
  readonly snapshotDate: string | null;

  /** Si la cartera tiene apertura declarada. Sin ella, «ganado» no se puede calcular. */
  readonly hasOpening: boolean;
}

/** El informe completo. */
export interface InformeDeRendimiento {
  readonly rows: readonly FilaDeRendimiento[];

  /** Los totales, o nulo si las filas no comparten moneda: sumar dólares y euros no significa nada. */
  readonly totals: FilaDeRendimiento | null;

  readonly currencies: readonly string[];
}

/**
 * El informe de rendimiento.
 *
 * Se llama **«coste contra valor»** y no «aportado contra ganado» porque la aplicación no tiene cuenta de
 * efectivo: «lo que vale» son las posiciones y nada más. El otro nombre haría creer que incluye el dinero sin
 * invertir, y no lo incluye.
 */
@Injectable({ providedIn: 'root' })
export class InformesApi {
  private readonly http = inject(HttpClient);

  /** El informe de las carteras que puede ver quien tiene la sesión. */
  rendimiento(): Promise<InformeDeRendimiento> {
    return firstValueFrom(this.http.get<InformeDeRendimiento>('/api/reports/performance'));
  }
}
