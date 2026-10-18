import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Movimiento, NuevoMovimiento, ResumenDeCartera, VistaDeCartera } from './modelos';

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

  /**
   * Edita el nombre, el tipo de registro y la moneda base **de una vez**.
   *
   * Una sola llamada y no tres, y no es comodidad de la pantalla: con tres, una podria fallar y las otras
   * no, y la cartera quedaria a medio editar. Ademas el libro de auditoria tendria tres entradas para lo
   * que fue un solo cambio.
   */
  editar(
    id: string,
    nombre: string,
    tipoDeRegistro: string,
    monedaBase: string | null,
  ): Promise<ResumenDeCartera> {
    return firstValueFrom(
      this.http.put<ResumenDeCartera>(`/api/portfolios/${id}`, {
        name: nombre,
        registrationType: tipoDeRegistro,
        baseCurrency: monedaBase,
      }),
    );
  }

  /** Archiva una cartera: deja de contarse, pero no se pierde nada de lo que tiene. */
  archivar(id: string): Promise<ResumenDeCartera> {
    return firstValueFrom(this.http.post<ResumenDeCartera>(`/api/portfolios/${id}/archive`, {}));
  }

  /** La vuelve a poner en uso. */
  desarchivar(id: string): Promise<ResumenDeCartera> {
    return firstValueFrom(this.http.post<ResumenDeCartera>(`/api/portfolios/${id}/unarchive`, {}));
  }

  /**
   * Borra una cartera, **solo si no tiene posiciones**.
   *
   * Si las tiene, la API responde 409 y no borra nada. El motivo del fallo llega como codigo en el
   * `title` del problema, y es lo que se le enseña a la persona: «tiene posiciones, archívala».
   */
  borrar(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/portfolios/${id}`));
  }

  /**
   * Los movimientos de una cartera: aportaciones, retiros y ventas, del mas reciente al mas antiguo.
   *
   * Las **ventas** aparecen aqui aunque no se creen aqui: se crean al vender, porque ademas reducen la
   * posicion. Es el registro unico, y por eso el informe puede sumar lo que entra, lo que sale y lo vendido.
   */
  movimientos(id: string): Promise<Movimiento[]> {
    return firstValueFrom(this.http.get<Movimiento[]>(`/api/portfolios/${id}/movements`));
  }

  /**
   * Registra una aportacion o un retiro.
   *
   * Una venta **no** se registra por aqui: se registra al vender. La API devuelve 400 si se intenta, para que
   * no pueda existir una venta sin su reduccion de posicion.
   */
  anotarMovimiento(id: string, movimiento: NuevoMovimiento): Promise<Movimiento> {
    return firstValueFrom(
      this.http.post<Movimiento>(`/api/portfolios/${id}/movements`, {
        kind: movimiento.tipo,
        amount: movimiento.importe,
        currency: movimiento.moneda,
        occurredOn: movimiento.dia,
        notes: movimiento.notas,
      }),
    );
  }
}
