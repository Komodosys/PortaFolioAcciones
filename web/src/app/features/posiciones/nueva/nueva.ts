import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { codigoDe } from '../../../core/api/interceptores';
import { Instrumento } from '../../../core/api/instrumentos/modelos';
import { Idioma } from '../../../core/i18n/idioma';
import { BuscadorInstrumento } from '../../../shared/buscador-instrumento/buscador-instrumento';
import { formatearImporte } from '../../../shared/formato/numeros';
import { PosicionesApi } from '../../../core/api/posiciones/posiciones.api';

/** Un lote mientras se esta escribiendo. Los numeros son texto hasta que se mandan. */
interface LoteEnEdicion {
  cantidad: string;
  costeUnitario: string;
  fecha: string;
  exactitud: string;
}

/**
 * El formulario de alta de una posicion.
 *
 * Dos pasos: **elegir el instrumento** y **decir las compras**. Se separan porque lo segundo depende de lo
 * primero: la moneda del coste es la del instrumento, y no se puede pedir un coste sin saber en que moneda.
 */
@Component({
  selector: 'app-nueva-posicion',
  imports: [FormsModule, TranslocoDirective, BuscadorInstrumento],
  templateUrl: './nueva.html',
  styleUrl: './nueva.scss',
})
export class NuevaPosicion {
  private readonly api = inject(PosicionesApi);
  private readonly router = inject(Router);
  private readonly idioma = inject(Idioma);

  /** La cartera donde se añade, que llega de la direccion. */
  readonly id = input.required<string>();

  readonly instrumento = signal<Instrumento | null>(null);

  /** Las compras. Empieza con una en blanco y se pueden añadir mas. */
  readonly lotes = signal<LoteEnEdicion[]>([]);

  notas = '';

  readonly enviando = signal(false);
  readonly fallo = signal<string | null>(null);

  /** La moneda en la que va el coste: la del instrumento. */
  readonly moneda = computed(() => this.instrumento()?.quoteCurrency ?? '');

  readonly totales = computed(() => {
    const suma = this.lotes().reduce(
      (acumulado, lote) => acumulado + numero(lote.cantidad) * numero(lote.costeUnitario),
      0,
    );

    return formatearImporte(suma, this.moneda() || 'CAD', this.idioma.activo());
  });

  /** Se elige el instrumento y se empieza con una compra en blanco. */
  elegir(instrumento: Instrumento): void {
    this.instrumento.set(instrumento);
    this.fallo.set(null);
    this.lotes.set([this.loteVacio()]);
  }

  cambiarInstrumento(): void {
    this.instrumento.set(null);
    this.lotes.set([]);
    this.fallo.set(null);
  }

  anadirLote(): void {
    this.lotes.update((lotes) => [...lotes, this.loteVacio()]);
  }

  quitarLote(indice: number): void {
    this.lotes.update((lotes) => lotes.filter((_, i) => i !== indice));
  }

  actualizarLote(indice: number, campo: keyof LoteEnEdicion, valor: string): void {
    this.lotes.update((lotes) =>
      lotes.map((lote, i) => (i === indice ? { ...lote, [campo]: valor } : lote)),
    );
  }

  async guardar(): Promise<void> {
    const instrumento = this.instrumento();

    if (!instrumento || this.enviando()) {
      return;
    }

    const lotes = this.lotes();

    if (lotes.length === 0) {
      this.fallo.set('posiciones.faltaUnaCompra');
      return;
    }

    // Se comprueba aqui para no gastar una peticion. La API lo valida de verdad.
    const malFormado = lotes.some(
      (lote) => numero(lote.cantidad) <= 0 || numero(lote.costeUnitario) < 0 || !lote.fecha,
    );

    if (malFormado) {
      this.fallo.set('posiciones.compraMalFormada');
      return;
    }

    this.enviando.set(true);
    this.fallo.set(null);

    try {
      await this.api.crearEnCartera(
        this.id(),
        instrumento.id,
        lotes.map((lote) => ({
          quantity: numero(lote.cantidad),
          costPerUnit: numero(lote.costeUnitario),
          purchaseDate: lote.fecha,
          dateAccuracy: lote.exactitud,
        })),
        this.notas.trim() || null,
      );

      await this.router.navigate(['/carteras', this.id()]);
    } catch (error: unknown) {
      this.fallo.set(
        error instanceof HttpErrorResponse ? (codigoDe(error) ?? 'comun.error') : 'comun.error',
      );
    } finally {
      this.enviando.set(false);
    }
  }

  cancelar(): void {
    void this.router.navigate(['/carteras', this.id()]);
  }

  private loteVacio(): LoteEnEdicion {
    return { cantidad: '', costeUnitario: '', fecha: '', exactitud: 'Exact' };
  }
}

/**
 * Convierte lo que se escribe a numero.
 *
 * Se acepta **la coma y el punto** como separador decimal: quien escribe en español teclea `165,54` y quien
 * escribe en ingles teclea `165.54`, y rechazar una de las dos formas seria molestar por una convencion que
 * la persona no eligio.
 */
function numero(texto: string): number {
  const limpio = texto.trim().replace(',', '.');

  if (!limpio) {
    return Number.NaN;
  }

  const valor = Number(limpio);
  return Number.isFinite(valor) ? valor : Number.NaN;
}
