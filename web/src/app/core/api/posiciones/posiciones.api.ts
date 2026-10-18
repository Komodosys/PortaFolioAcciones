import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** Una compra, tal como se manda. */
export interface LoteAEnviar {
  readonly quantity: number;
  readonly costPerUnit: number;
  readonly purchaseDate: string;

  /**
   * Si la fecha es exacta o estimada.
   *
   * Importa: los extractos de un año entero no siempre dan la fecha de cada compra, y **fingir que se sabe
   * cuando no se sabe es peor que decirlo**.
   */
  readonly dateAccuracy: string;
}

/** Una posición, tal como la devuelve la API. */
export interface Posicion {
  readonly id: string;
  readonly portfolioId: string;
  readonly instrumentId: string;
  readonly symbol: string;
  readonly instrumentName: string;
  readonly quantity: number;
  readonly averageCostPerUnit: number;
  readonly costCurrency: string;
  readonly totalCost: number;
  readonly isClosed: boolean;
  readonly manualPriceAmount: number | null;
  readonly manualPriceCurrency: string | null;
  readonly manualPriceDate: string | null;
  readonly notes: string | null;
  readonly hasApproximateDates: boolean;
  readonly lots: readonly {
    readonly id: string;
    readonly quantity: number;
    readonly costPerUnit: number;
    readonly purchaseDate: string;
    readonly dateAccuracy: string;
    readonly notes: string | null;
  }[];
}

/** La cuenta a la que pertenece una posicion: como se llama y de quien es. */
export interface CuentaDeLaPosicion {
  readonly portfolioId: string;
  readonly portfolioName: string;
  readonly ownerUserId: string;
  readonly ownerDisplayName: string;
}

/**
 * El acceso a la API de posiciones.
 *
 * **Ninguna direccion lleva el identificador de la persona**: quien es se sabe por la sesion, en el
 * servidor.
 */
@Injectable({ providedIn: 'root' })
export class PosicionesApi {
  private readonly http = inject(HttpClient);

  /**
   * Da de alta una posicion en una cartera.
   *
   * Si esa cartera ya tiene una posicion de ese instrumento, la API **añade las compras a la que hay** en
   * vez de crear otra: dos posiciones del mismo instrumento en la misma cartera se sumarian mal y nadie
   * sabria por que.
   */
  crearEnCartera(
    portfolioId: string,
    instrumentId: string,
    lotes: readonly LoteAEnviar[],
    notas: string | null,
  ): Promise<Posicion> {
    return firstValueFrom(
      this.http.post<Posicion>(`/api/portfolios/${portfolioId}/positions`, {
        instrumentId,
        lots: lotes,
        notes: notas,
      }),
    );
  }

  /** Una posicion concreta. */
  obtener(id: string): Promise<Posicion> {
    return firstValueFrom(this.http.get<Posicion>(`/api/positions/${id}`));
  }

  /**
   * Aplica un desdoblamiento, o anota que ya esta reflejado.
   *
   * Las dos cosas son necesarias y no es una comodidad: quien dio de alta sus posiciones copiando un
   * estado de cuenta **posterior** al desdoblamiento ya tiene las cifras buenas, y aplicar entonces las
   * multiplicaria por segunda vez. Sin la segunda opcion, esa persona solo podria elegir entre estropear
   * sus datos o convivir con un aviso que no puede quitar.
   */
  aplicarDesdoblamiento(
    id: string,
    ratio: number,
    fecha: string,
    aplicar: boolean,
  ): Promise<Posicion> {
    return firstValueFrom(
      this.http.post<Posicion>(`/api/positions/${id}/split`, {
        ratio,
        date: fecha,
        apply: aplicar,
      }),
    );
  }

  /**
   * La cuenta a la que pertenece la posicion: como se llama y de quien es.
   *
   * Se pide aparte del detalle porque es contexto de la **cuenta**, no de la posicion. La pantalla lo
   * necesita para poder decir en que cuenta y de quien se esta trabajando.
   */
  cuentaDe(id: string): Promise<CuentaDeLaPosicion> {
    return firstValueFrom(this.http.get<CuentaDeLaPosicion>(`/api/positions/${id}/account`));
  }

  /**
   * Anade una compra a una posicion que ya existe.
   *
   * El coste medio se recalcula solo en el servidor: **el cliente no suma nada**.
   */
  anadirCompra(id: string, lote: LoteAEnviar): Promise<Posicion> {
    return firstValueFrom(this.http.post<Posicion>(`/api/positions/${id}/lots`, lote));
  }

  /**
   * Vende una cantidad.
   *
   * El servidor reparte la venta **proporcionalmente entre todos los lotes**, que es lo que hace el metodo
   * de coste medio del broker y lo que **conserva el coste por unidad**. Aqui no se elige que lote se
   * vende: elegirlo daria un coste medio distinto del que declara el estado de cuenta.
   */
  vender(id: string, cantidad: number): Promise<Posicion> {
    return firstValueFrom(this.http.post<Posicion>(`/api/positions/${id}/sell`, { quantity: cantidad }));
  }

  /**
   * Pone un precio a mano, con su fecha y su moneda.
   *
   * Manda sobre cualquier fuente: es una decision de quien tiene la posicion, y por eso el servidor no la
   * pisa con un precio de mercado.
   */
  ponerPrecioManual(
    id: string,
    importe: number,
    moneda: string,
    fecha: string,
  ): Promise<Posicion> {
    return firstValueFrom(
      this.http.put<Posicion>(`/api/positions/${id}/manual-price`, {
        amount: importe,
        currency: moneda,
        date: fecha,
      }),
    );
  }

  /** Cierra la posicion, conservando lo que habia costado. */
  cerrar(id: string): Promise<Posicion> {
    return firstValueFrom(this.http.post<Posicion>(`/api/positions/${id}/close`, {}));
  }

  /** Vuelve a abrir una posicion cerrada. */
  reabrir(id: string): Promise<Posicion> {
    return firstValueFrom(this.http.post<Posicion>(`/api/positions/${id}/reopen`, {}));
  }

  /** Cambia las notas. */
  ponerNotas(id: string, notas: string | null): Promise<Posicion> {
    return firstValueFrom(this.http.put<Posicion>(`/api/positions/${id}/notes`, { notes: notas }));
  }

  /**
   * Borra una posicion dada de alta por error.
   *
   * **Sube el efectivo por el coste de lo borrado**, porque el saldo se calcula restando los lotes. Es lo
   * correcto si la posicion nunca existio: ese dinero no salio de la cuenta.
   *
   * El servidor contesta **409** si la posicion tiene ventas: una venta es un hecho y se deshace antes.
   */
  borrar(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/positions/${id}`));
  }
}
