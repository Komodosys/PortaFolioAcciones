import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** Un cambio de un campo, con lo que había antes y lo que hay después. */
export interface CambioDeCampo {
  readonly field: string;
  readonly oldValue: string | null;
  readonly newValue: string | null;
}

/** Una anotación del libro de auditoría. */
export interface Anotacion {
  readonly id: string;
  readonly occurredAtUtc: string;
  readonly actorName: string;
  readonly ownerName: string;
  readonly action: string;
  readonly entityType: string;
  readonly entityId: string;
  readonly changes: readonly CambioDeCampo[];
  readonly reason: string | null;
  readonly isSomeoneElseData: boolean;
}

/** Una persona cuyos datos aparecen en el libro. */
export interface PersonaDelLibro {
  readonly id: string;
  readonly displayName: string;
}

/** Qué se quiere ver del libro. */
export interface FiltroDeAuditoria {
  readonly ownerUserId?: string | null;
  readonly entityType?: string | null;
  readonly from?: string | null;
  readonly to?: string | null;
  readonly onlySomeoneElseData?: boolean;
  readonly limit?: number;
}

/**
 * El libro de auditoría, **de solo lectura**.
 *
 * No hay método para escribir ni para borrar, y no es un olvido: el libro es de solo añadir. Un registro
 * que se puede modificar no es un registro, y la API tampoco ofrece esas direcciones.
 */
@Injectable({ providedIn: 'root' })
export class AuditoriaApi {
  private readonly http = inject(HttpClient);

  /** Las anotaciones que cumplen el filtro, de la más reciente a la más antigua. */
  leer(filtro: FiltroDeAuditoria): Promise<Anotacion[]> {
    let parametros = new HttpParams();

    if (filtro.ownerUserId) {
      parametros = parametros.set('ownerUserId', filtro.ownerUserId);
    }

    if (filtro.entityType) {
      parametros = parametros.set('entityType', filtro.entityType);
    }

    if (filtro.from) {
      parametros = parametros.set('from', filtro.from);
    }

    if (filtro.to) {
      parametros = parametros.set('to', filtro.to);
    }

    if (filtro.onlySomeoneElseData) {
      parametros = parametros.set('onlySomeoneElseData', 'true');
    }

    parametros = parametros.set('limit', String(filtro.limit ?? 200));

    return firstValueFrom(this.http.get<Anotacion[]>('/api/admin/audit', { params: parametros }));
  }

  /** Las personas **cuyos datos** aparecen en el libro, para poder filtrar por ellas. */
  personas(): Promise<PersonaDelLibro[]> {
    return firstValueFrom(this.http.get<PersonaDelLibro[]>('/api/admin/audit/people'));
  }
}
