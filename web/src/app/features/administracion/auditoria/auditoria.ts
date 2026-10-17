import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { codigoDe } from '../../../core/api/interceptores';
import { Idioma } from '../../../core/i18n/idioma';
import { formatearFecha } from '../../../shared/formato/numeros';
import { Anotacion, AuditoriaApi, PersonaDelLibro } from '../auditoria.api';

/** Una anotación con sus textos ya formateados. */
interface Fila {
  readonly id: string;
  readonly cuando: string;
  readonly quien: string;
  readonly aQuien: string;
  readonly accion: string;
  readonly entidad: string;
  readonly esAjena: boolean;
  readonly motivo: string | null;
  readonly cambios: readonly {
    readonly campo: string;
    readonly antes: string;
    readonly despues: string;
  }[];
}

/**
 * El libro de auditoría.
 *
 * Existe porque el administrador puede modificar datos de otras personas
 * ([ADR-014](../../../../DOCUMENTACION/decisiones/ADR-014-administrador-escritura-con-auditoria.md)), y la
 * auditoría es **la contrapartida de ese poder**, no un adorno.
 *
 * La pantalla está construida alrededor de una sola pregunta: **¿qué se ha tocado que no fuera mío?** Por
 * eso el filtro de «solo datos de otras personas» viene **activado de entrada**, y por eso las anotaciones
 * ajenas se distinguen a simple vista. Enseñar el libro entero sin jerarquía convierte la revisión en un
 * trabajo que nadie hace.
 */
@Component({
  selector: 'app-auditoria',
  imports: [FormsModule, TranslocoDirective],
  templateUrl: './auditoria.html',
  // El estilo vive en la plantilla, con clases de Tailwind: no queda SCSS que mantener.
  host: { class: 'block' },
})
export class Auditoria {
  private readonly api = inject(AuditoriaApi);
  private readonly idioma = inject(Idioma);

  readonly anotaciones = signal<Anotacion[]>([]);
  readonly personas = signal<PersonaDelLibro[]>([]);
  readonly cargando = signal(true);
  readonly fallo = signal<string | null>(null);

  // Filtros. Arrancan en lo que de verdad se quiere revisar.
  persona = '';
  desde = '';
  hasta = '';
  soloAjenas = true;

  constructor() {
    void this.cargar();
  }

  readonly filas = computed<readonly Fila[]>(() => {
    const idioma = this.idioma.activo();

    return this.anotaciones().map((anotacion) => ({
      id: anotacion.id,
      cuando: fechaYHora(anotacion.occurredAtUtc, idioma),
      quien: anotacion.actorName,
      aQuien: anotacion.ownerName,
      accion: `auditoria.accion.${anotacion.action}`,
      entidad: anotacion.entityType,
      esAjena: anotacion.isSomeoneElseData,
      motivo: anotacion.reason,
      cambios: anotacion.changes.map((cambio) => ({
        campo: cambio.field,
        // Un valor nulo se enseña como «vacío» y no como una celda en blanco: en blanco no se distingue
        // de «no se registró».
        antes: cambio.oldValue ?? '',
        despues: cambio.newValue ?? '',
      })),
    }));
  });

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.fallo.set(null);

    try {
      this.anotaciones.set(
        await this.api.leer({
          ownerUserId: this.persona || null,
          // Las fechas vienen de un `input type="date"` (`2026-10-15`). Se convierten a instantes: sin la
          // hora, el «hasta» dejaría fuera todo lo del propio día, que es justo lo que se quiere ver.
          from: this.desde ? `${this.desde}T00:00:00Z` : null,
          to: this.hasta ? `${this.hasta}T23:59:59Z` : null,
          onlySomeoneElseData: this.soloAjenas,
          limit: 500,
        }),
      );

      // La lista de personas se pide una sola vez: el libro no cambia mientras se mira.
      if (this.personas().length === 0) {
        this.personas.set(await this.api.personas());
      }
    } catch (error: unknown) {
      this.fallo.set(
        error instanceof HttpErrorResponse ? (codigoDe(error) ?? 'comun.error') : 'comun.error',
      );
    } finally {
      this.cargando.set(false);
    }
  }

  async limpiar(): Promise<void> {
    this.persona = '';
    this.desde = '';
    this.hasta = '';
    this.soloAjenas = true;

    await this.cargar();
  }
}

/**
 * Escribe un instante con su hora.
 *
 * La hora importa aquí y no en el resto de la aplicación: dos cambios del mismo día en la misma cuenta se
 * distinguen por la hora, y sin ella el orden no se entiende.
 */
function fechaYHora(iso: string, idioma: string): string {
  const momento = new Date(iso);

  if (Number.isNaN(momento.getTime())) {
    return iso;
  }

  const dia = formatearFecha(iso.slice(0, 10), idioma);

  return `${dia} ${momento.toLocaleTimeString(idioma, { hour: '2-digit', minute: '2-digit' })}`;
}
