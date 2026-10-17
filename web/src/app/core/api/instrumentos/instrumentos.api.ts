import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AltaDeInstrumento, Instrumento, ResultadoDeFuente } from './modelos';

/**
 * El catalogo de instrumentos y la busqueda en la fuente de mercado.
 *
 * Vive en `core` y no en una funcionalidad porque **lo usan varias**: el buscador del formulario de
 * posiciones, y mañana cualquier pantalla que necesite elegir un instrumento. La regla del proyecto es que
 * una funcionalidad no importe de otra, asi que lo compartido va aqui.
 *
 * Hay **dos busquedas distintas** y no se mezclan:
 * <list type="bullet">
 *   <item>La del **catalogo**, que es lo que ya existe en esta casa. Es instantanea y no depende de nadie.</item>
 *   <item>La de **fuera**, para dar de alta algo nuevo. Depende de un servicio ajeno y puede tardar o no
 *   responder.</item>
 * </list>
 * Se separan porque lo que se quiere saber es distinto: «¿ya tengo esto?» frente a «¿cómo se llama esto
 * ahí fuera?».
 */
@Injectable({ providedIn: 'root' })
export class InstrumentosApi {
  private readonly http = inject(HttpClient);

  /** Busca en el catalogo de la casa. */
  buscar(texto: string): Promise<Instrumento[]> {
    return firstValueFrom(
      this.http.get<Instrumento[]>('/api/instruments', { params: { q: texto } }),
    );
  }

  /** Un instrumento concreto del catalogo. */
  obtener(id: string): Promise<Instrumento> {
    return firstValueFrom(this.http.get<Instrumento>(`/api/instruments/${id}`));
  }

  /**
   * Busca **fuera**, en la fuente de mercado.
   *
   * Devuelve una lista vacia cuando no encuentra nada, y eso **no es un error**: «no he encontrado nada»
   * es una respuesta legitima a una busqueda.
   */
  buscarFuera(texto: string): Promise<ResultadoDeFuente[]> {
    return firstValueFrom(
      this.http.get<ResultadoDeFuente[]>('/api/instruments/lookup', { params: { q: texto } }),
    );
  }

  /**
   * Da de alta un instrumento.
   *
   * Si ya existe uno con esa identidad —simbolo, bolsa y moneda—, la API **devuelve el que hay** en vez de
   * crear otro. Es lo que pasa cuando dos personas de la casa compran el mismo valor, y lo correcto es que
   * las dos posiciones apunten al mismo instrumento.
   */
  crear(datos: AltaDeInstrumento): Promise<Instrumento> {
    return firstValueFrom(this.http.post<Instrumento>('/api/instruments', datos));
  }
}
