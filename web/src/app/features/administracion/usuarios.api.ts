import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** Una cuenta, tal y como la ve el administrador. */
export interface Cuenta {
  readonly id: string;
  readonly userName: string;
  readonly displayName: string;
  readonly baseCurrency: string;
  readonly language: string;
  readonly isAdmin: boolean;
  readonly mustChangePassword: boolean;
  readonly isActive: boolean;
}

/** Lo que se manda para dar de alta una cuenta. */
export interface AltaDeCuenta {
  readonly userName: string;
  readonly displayName: string;
  readonly password: string;
  readonly baseCurrency: string;
  readonly language: string;
  readonly isAdmin: boolean;
}

/**
 * Una contraseña temporal.
 *
 * Se devuelve **una sola vez** y no se guarda en claro en ningún sitio: si se pierde, se genera otra. Por eso
 * el tipo no tiene un método para volver a leerla.
 */
export interface ClaveTemporal {
  readonly temporaryPassword: string;
  readonly expiresAtUtc: string;
  readonly mustChangePassword: boolean;
}

/**
 * Las cuentas de la casa, **para el administrador**.
 *
 * Todas las direcciones de aquí son de administración: la API las rechaza con un 403 a quien no lo sea. Esa es
 * la protección de verdad; la guarda de la ruta solo evita enseñar una pantalla que no va a funcionar.
 *
 * **No hay método para leer una contraseña**, y no es un olvido: no se guardan en claro. Lo único que se puede
 * hacer es generar una temporal, que es justo lo que hace falta cuando alguien pierde la suya.
 */
@Injectable({ providedIn: 'root' })
export class UsuariosApi {
  private readonly http = inject(HttpClient);

  /** Quién tiene acceso, ordenado por nombre. */
  listar(): Promise<Cuenta[]> {
    return firstValueFrom(this.http.get<Cuenta[]>('/api/admin/users'));
  }

  /**
   * Da de alta una cuenta.
   *
   * La contraseña que se manda aquí es **provisional y no se usa**: la pantalla la genera al azar y acto
   * seguido pide una temporal, que es la que se entrega en mano. La API la exige al crear porque una cuenta
   * sin credencial no existe, pero nadie llega a conocerla.
   */
  crear(alta: AltaDeCuenta): Promise<{ id: string }> {
    return firstValueFrom(this.http.post<{ id: string }>('/api/admin/users', alta));
  }

  /** Genera una contraseña temporal y la devuelve. La anterior deja de servir. */
  claveTemporal(id: string): Promise<ClaveTemporal> {
    return firstValueFrom(
      this.http.post<ClaveTemporal>(`/api/admin/users/${id}/reset-password`, {}),
    );
  }
}

/**
 * Una contraseña provisional, al azar, que cumple la política de la aplicación.
 *
 * Se genera en el navegador y **no se le enseña a nadie**: se manda al crear la cuenta y se sustituye en el
 * acto por una temporal. Existe porque la API no admite crear una cuenta sin credencial.
 */
export function claveProvisional(): string {
  const bytes = new Uint8Array(24);
  crypto.getRandomValues(bytes);

  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');

  // Con mayúscula, minúscula y número, que es lo que pide la política. El `!` da la longitud de sobra.
  return `${hex}-Aa1!`;
}
