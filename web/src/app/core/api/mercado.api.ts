import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** Lo que ha pasado al refrescar las cotizaciones. */
export interface RefrescoDeCotizaciones {
  readonly instrumentos: number;
  readonly actualizados: number;

  /** De cuántos no se ha obtenido precio. **No es un fallo**: la fuente puede no responder. */
  readonly sinPrecio: number;
}

/** Lo que ha pasado al recoger el cierre del día. */
export interface RecogidaDiaria {
  readonly instrumentos: number;
  readonly guardados: number;
  readonly yaEstaban: number;
  readonly sinPrecio: number;
  readonly carteras: number;
  readonly fotos: number;
}

/**
 * Los refrescos de precios pedidos a mano.
 *
 * **Solo el administrador.** Las dos operaciones llaman a una fuente externa gratuita y escriben precios, y
 * en una aplicación que se deja abierta en una pestaña, un botón que cualquiera puede pulsar en bucle acaba
 * en un bloqueo que deja sin precios a todos.
 *
 * La diferencia entre las dos es lo que las hace seguras: refrescar cotizaciones solo cambia «lo último que
 * se sabe»; recoger el cierre escribe el cierre del día, y **antes de que las bolsas hayan cerrado eso es en
 * realidad el precio del momento**.
 */
@Injectable({ providedIn: 'root' })
export class MercadoApi {
  private readonly http = inject(HttpClient);

  /**
   * Vuelve a pedir la cotización de cada instrumento.
   *
   * Se puede llamar a cualquier hora. La API responde **429** si se pide dos veces seguidas, y el motivo
   * llega como código en el `title`.
   */
  refrescarCotizaciones(): Promise<RefrescoDeCotizaciones> {
    return firstValueFrom(this.http.post<RefrescoDeCotizaciones>('/api/market/refresh-quotes', {}));
  }

  /**
   * Ejecuta la recogida diaria ahora: cierre, tipos de cambio y foto de las carteras.
   *
   * **Antes del cierre de las bolsas guarda como cierre el precio del momento.** Se corrige solo cuando la
   * recogida de verdad vuelve a pasar, pero mientras tanto el histórico dice algo que no es.
   */
  recogerElCierre(): Promise<RecogidaDiaria> {
    return firstValueFrom(this.http.post<RecogidaDiaria>('/api/market/collect', {}));
  }
}
