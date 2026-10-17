import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { codigoDe } from '../../../core/api/interceptores';
import { Idioma } from '../../../core/i18n/idioma';
import { formatearFecha } from '../../../shared/formato/numeros';
import { ClaveTemporal, Cuenta, UsuariosApi, claveProvisional } from '../usuarios.api';

/** Una cuenta con sus textos ya resueltos. */
interface Fila {
  readonly id: string;
  readonly userName: string;
  readonly displayName: string;
  readonly baseCurrency: string;
  readonly language: string;
  readonly isAdmin: boolean;
  readonly pendienteDeCambiar: boolean;
}

/** La contraseña temporal que se acaba de generar, para enseñarla una vez. */
interface ClaveRecienGenerada {
  readonly persona: string;
  readonly clave: string;
  readonly caduca: string | null;
}

/** Lo que se está escribiendo en el formulario de alta. */
interface Borrador {
  displayName: string;
  userName: string;
  baseCurrency: string;
  language: string;
  isAdmin: boolean;
}

/**
 * Las cuentas de la casa.
 *
 * **Solo la ve el administrador**, y no por comodidad: aquí se crean accesos a los datos de la familia y se
 * generan credenciales. La guarda de la ruta lo comprueba, y la API lo vuelve a comprobar en cada petición.
 *
 * Dos decisiones que están en la pantalla y no en un comentario:
 *
 * - **La contraseña no la elige el administrador.** La aplicación genera una temporal y se la enseña una sola
 *   vez para que la entregue en mano. Si la eligiera él, acabaría poniendo la misma para todos.
 * - **La temporal se enseña una vez y no se puede volver a mirar.** No se guarda en claro en ningún sitio, así
 *   que si se pierde se genera otra. El botón de cada fila está justo para eso.
 */
@Component({
  selector: 'app-usuarios',
  imports: [FormsModule, TranslocoDirective],
  templateUrl: './usuarios.html',
  // El estilo vive en la plantilla, con clases de Tailwind: no queda SCSS que mantener.
  host: { class: 'block' },
})
export class Usuarios {
  private readonly api = inject(UsuariosApi);
  private readonly idioma = inject(Idioma);

  readonly cuentas = signal<Cuenta[]>([]);
  readonly cargando = signal(true);
  readonly fallo = signal<string | null>(null);

  /** Si el formulario de alta está desplegado. */
  readonly creando = signal(false);
  readonly guardando = signal(false);
  readonly falloDeAlta = signal<string | null>(null);

  /** La clave temporal recién generada, o nula si no hay ninguna pendiente de entregar. */
  readonly claveNueva = signal<ClaveRecienGenerada | null>(null);

  /** El identificador de la cuenta cuya clave se está generando, para deshabilitar su botón. */
  readonly generandoPara = signal<string | null>(null);

  borrador: Borrador = this.borradorVacio();

  constructor() {
    void this.cargar();
  }

  readonly filas = computed<readonly Fila[]>(() =>
    this.cuentas().map((cuenta) => ({
      id: cuenta.id,
      userName: cuenta.userName,
      displayName: cuenta.displayName,
      baseCurrency: cuenta.baseCurrency,
      language: cuenta.language,
      isAdmin: cuenta.isAdmin,
      // Una cuenta con la temporal sin cambiar **no ha entrado todavía**. Eso es distinto de una que se usa, y
      // es lo que hace falta saber para decidir si hay que volver a darle la clave a alguien.
      pendienteDeCambiar: cuenta.mustChangePassword,
    })),
  );

  readonly hayAdministradores = computed(() => this.cuentas().filter((c) => c.isAdmin).length);

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.fallo.set(null);

    try {
      this.cuentas.set(await this.api.listar());
    } catch (error: unknown) {
      this.fallo.set(this.claveDe(error));
    } finally {
      this.cargando.set(false);
    }
  }

  abrirFormulario(): void {
    this.borrador = this.borradorVacio();
    this.falloDeAlta.set(null);
    this.creando.set(true);
  }

  cancelarFormulario(): void {
    this.creando.set(false);
    this.falloDeAlta.set(null);
  }

  async crear(): Promise<void> {
    if (!this.borrador.displayName.trim() || !this.borrador.userName.trim()) {
      this.falloDeAlta.set('usuarios.faltanDatos');
      return;
    }

    this.guardando.set(true);
    this.falloDeAlta.set(null);

    try {
      const alta = await this.api.crear({
        userName: this.borrador.userName.trim(),
        displayName: this.borrador.displayName.trim(),
        password: claveProvisional(),
        baseCurrency: this.borrador.baseCurrency.trim().toUpperCase() || 'CAD',
        language: this.borrador.language,
        isAdmin: this.borrador.isAdmin,
      });

      this.creando.set(false);

      // Y acto seguido la temporal, que es la que se entrega. La provisional no la conoce nadie.
      await this.generarPara(alta.id, this.borrador.displayName.trim());
      await this.cargar();
    } catch (error: unknown) {
      this.falloDeAlta.set(this.claveDe(error));
    } finally {
      this.guardando.set(false);
    }
  }

  /** Genera una temporal para una cuenta y la deja en pantalla para entregarla. */
  async generar(para: Fila): Promise<void> {
    this.generandoPara.set(para.id);

    try {
      await this.generarPara(para.id, para.displayName);
      await this.cargar();
    } catch (error: unknown) {
      this.fallo.set(this.claveDe(error));
    } finally {
      this.generandoPara.set(null);
    }
  }

  /** Se cierra el aviso de la clave. A partir de aquí ya no se puede volver a mirar. */
  ocultarClave(): void {
    this.claveNueva.set(null);
  }

  claveDe(error: unknown): string {
    return error instanceof HttpErrorResponse ? (codigoDe(error) ?? 'comun.error') : 'comun.error';
  }

  private async generarPara(id: string, persona: string): Promise<void> {
    const temporal: ClaveTemporal = await this.api.claveTemporal(id);

    this.claveNueva.set({
      persona,
      clave: temporal.temporaryPassword,
      caduca: temporal.expiresAtUtc
        ? formatearFecha(temporal.expiresAtUtc.slice(0, 10), this.idioma.activo())
        : null,
    });
  }

  private borradorVacio(): Borrador {
    return {
      displayName: '',
      userName: '',
      baseCurrency: 'CAD',
      language: 'es',
      isAdmin: false,
    };
  }
}
