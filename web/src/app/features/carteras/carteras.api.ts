import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ResumenDeCartera, VistaDeCartera } from './modelos';

/**
 * El acceso a la API de carteras.
 *
 * **Ninguna direccion lleva el identificador de la persona.** Quien es se sabe por la sesion, en el
 * servidor: si la direccion lo llevara, bastaria con cambiarlo para pedir la cartera de otro. Esa es la
 * razon de que aqui no haya ningun parametro de usuario.
 */
@Injectable({ providedIn: 'root' })
export class CarterasApi {
  private readonly http = inject(HttpClient);

  /** Las carteras que puede ver quien tiene la sesion. */
  listar(incluirArchivadas = false): Promise<ResumenDeCartera[]> {
    return firstValueFrom(
      this.http.get<ResumenDeCartera[]>('/api/portfolios', {
        params: { includeArchived: incluirArchivadas },
      }),
    );
  }

  /**
   * Crea una cartera.
   *
   * El dueño lo pone el servidor a partir de la sesion: aqui no se manda, y ese es justo el punto.
   *
   * El tipo de registro es **obligatorio**: la API lo rechaza si falta, y no le pone un valor por defecto.
   * Es a proposito — etiquetar en silencio una cuenta de jubilacion como no registrada es peor que no
   * dejar crearla.
   */
  crear(
    nombre: string,
    tipoDeRegistro: string,
    monedaBase: string | null,
  ): Promise<ResumenDeCartera> {
    return firstValueFrom(
      this.http.post<ResumenDeCartera>('/api/portfolios', {
        name: nombre,
        registrationType: tipoDeRegistro,
        baseCurrency: monedaBase,
      }),
    );
  }

  /**
   * La vista de una cartera: sus posiciones y sus totales.
   *
   * Los totales **vienen calculados del servidor**, ya convertidos a la moneda base. El cliente no suma
   * nada: si lo hiciera, dos pantallas podrian dar cifras distintas de lo mismo.
   */
  vista(id: string): Promise<VistaDeCartera> {
    return firstValueFrom(this.http.get<VistaDeCartera>(`/api/portfolios/${id}/view`));
  }

  /** Una cartera concreta. */
  obtener(id: string): Promise<ResumenDeCartera> {
    return firstValueFrom(this.http.get<ResumenDeCartera>(`/api/portfolios/${id}`));
  }
}
