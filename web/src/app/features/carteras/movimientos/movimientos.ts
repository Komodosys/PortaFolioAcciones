import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { codigoDe } from '../../../core/api/interceptores';
import { Idioma } from '../../../core/i18n/idioma';
import { formatearImporte } from '../../../shared/formato/numeros';
import { CarterasApi } from '../carteras.api';
import { Movimiento, SaldoDeEfectivo } from '../modelos';

/**
 * Los movimientos de una cartera: aportaciones, retiros y ventas.
 *
 * **Solo se anotan aportaciones y retiros.** Una venta no es algo que se declare: es lo que pasa al vender,
 * y se registra sola en ese momento. Dejar anotarla aquí permitiría una venta sin su reducción de posición, y
 * el registro y la posición dirían cosas distintas.
 *
 * Está en su propia pantalla y no dentro del detalle porque el detalle ya enseña la tabla de posiciones: una
 * segunda tabla y un formulario debajo la harían ilegible en el móvil.
 */
@Component({
  selector: 'app-movimientos-de-cartera',
  imports: [FormsModule, RouterLink, TranslocoDirective],
  templateUrl: './movimientos.html',
  host: { class: 'block' },
})
export class MovimientosDeCartera {
  private readonly api = inject(CarterasApi);
  private readonly idioma = inject(Idioma);
  private readonly transloco = inject(TranslocoService);

  readonly id = input.required<string>();

  readonly movimientos = signal<readonly Movimiento[]>([]);

  /** El saldo de efectivo, calculado por el servidor desde la apertura. */
  readonly saldo = signal<SaldoDeEfectivo | null>(null);

  /** Si se esta corrigiendo la apertura. */
  readonly corrigiendo = signal(false);

  /**
   * Lo que se esta escribiendo para corregirla.
   *
   * **En senales y no en campos.** La aplicacion es sin zona: un campo normal cambia y la pantalla no se
   * entera, asi que el formulario pareceria no responder.
   */
  readonly correccion = signal({ amount: 0, currency: 'CAD', occurredOn: '', notes: '' });

  /** La apertura guardada, para poder corregirla. */
  readonly aperturaActual = computed(() => this.movimientos().find((m) => m.kind === 'Opening') ?? null);

  /**
   * Borra un movimiento, preguntando antes.
   *
   * Se pregunta porque no hay forma de recuperarlo: el borrado es definitivo y la confirmacion cuesta un clic
   * menos que volver a anotar el movimiento.
   */
  async borrar(movimiento: Movimiento): Promise<void> {
    if (!confirm(this.transloco.translate('movimiento.borrarPregunta'))) {
      return;
    }

    try {
      await this.api.borrarMovimiento(this.id(), movimiento.id);
      await this.cargar();
    } catch (error) {
      this.fallo.set(this.claveDe(error));
    }
  }

  abrirCorreccion(): void {
    const actual = this.aperturaActual();

    this.correccion.set({
      amount: actual?.amount ?? 0,
      currency: actual?.currency ?? 'CAD',
      occurredOn: actual?.occurredOn ?? '',
      notes: actual?.notes ?? '',
    });

    this.corrigiendo.set(true);
  }

  cambiarCorreccion(campo: string, valor: unknown): void {
    this.correccion.update((actual) => ({ ...actual, [campo]: valor }));
  }

  async guardarCorreccion(): Promise<void> {
    const datos = this.correccion();
    this.guardando.set(true);

    try {
      await this.api.corregirApertura(this.id(), {
        tipo: 'Opening',
        importe: datos.amount,
        moneda: datos.currency,
        dia: datos.occurredOn || null,
        notas: datos.notes || null,
      });

      this.corrigiendo.set(false);
      await this.cargar();
    } catch (error) {
      this.fallo.set(this.claveDe(error));
    } finally {
      this.guardando.set(false);
    }
  }

  /**
   * Un importe escrito como se escribe el dinero: dos decimales y el código de la moneda.
   *
   * Se usa el formateador compartido, el mismo que el informe y el detalle. Antes esta pantalla escribía el
   * número crudo, y se veían cosas como «-37544.90991321296 CAD».
   *
   * Se llama mostrarImporte y no importe porque el componente ya tiene un campo importe, que es el del
   * formulario.
   */
  mostrarImporte(valor: number, moneda: string): string {
    return formatearImporte(valor, moneda, this.idioma.activo());
  }
  readonly cargando = signal(true);
  readonly fallo = signal<string | null>(null);

  /**
   * La carga va dentro de un effect y no en el cuerpo del constructor.
   *
   * **En el constructor, un input.required todavia no tiene valor**: Angular lanza al leerlo, el catch de
   * cargar lo recoge y la pantalla muestra «ha ocurrido un error» sin decir nada mas. Un effect se ejecuta
   * despues de que Angular asigne las entradas, y ademas vuelve a correr si cambia la cartera sin salir de la
   * pantalla.
   */
  constructor() {
    effect(() => {
      void this.cargar();
    });
  }

  /** El formulario. */
  readonly anotando = signal(false);
  readonly guardando = signal(false);
  readonly falloDeAlta = signal<string | null>(null);

  tipo = 'Deposit';
  importe: number | null = null;
  moneda = 'CAD';
  dia = '';
  notas = '';

  /**
   * Los tipos que se pueden anotar a mano. Las ventas vienen solas, al vender, y no están en la lista.
   *
   * Los traspasos van aparte de las aportaciones y los retiros a propósito: mueven efectivo igual, pero no son
   * dinero puesto por nadie, y contarlos como aportación diría en el informe que se aportó más de lo que se
   * aportó.
   */
  readonly tipos = [
    { valor: 'Deposit', clave: 'movimiento.tipos.Deposit' },
    { valor: 'Withdrawal', clave: 'movimiento.tipos.Withdrawal' },
    { valor: 'TransferIn', clave: 'movimiento.tipos.TransferIn' },
    { valor: 'TransferOut', clave: 'movimiento.tipos.TransferOut' },
    { valor: 'Opening', clave: 'movimiento.tipos.Opening' },
    { valor: 'Dividend', clave: 'movimiento.tipos.Dividend' },
    { valor: 'Commission', clave: 'movimiento.tipos.Commission' },
  ];

  /**
   * Lo que entró y lo que salió, en la moneda de cada movimiento.
   *
   * Se separan porque **una venta no es dinero aportado**: el dinero ya estaba dentro y solo cambió de forma.
   * Sumarla aquí haría creer que se puso mucho más de lo que se puso.
   */
  readonly aportado = computed(() =>
    this.movimientos()
      .filter((movimiento) => movimiento.kind === 'Deposit')
      .reduce((total, movimiento) => total + movimiento.amount, 0),
  );

  readonly retirado = computed(() =>
    this.movimientos()
      .filter((movimiento) => movimiento.kind === 'Withdrawal')
      .reduce((total, movimiento) => total + movimiento.amount, 0),
  );

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.fallo.set(null);

    try {
      this.movimientos.set(await this.api.movimientos(this.id()));

      // El saldo va aparte a proposito: si fallara, los movimientos ya cargados no se deben perder.
      try {
        this.saldo.set(await this.api.saldoDeEfectivo(this.id()));
      } catch {
        this.saldo.set(null);
      }
    } catch (error: unknown) {
      this.movimientos.set([]);
      this.fallo.set(this.claveDe(error));
    } finally {
      this.cargando.set(false);
    }
  }

  abrirFormulario(): void {
    this.anotando.set(true);
    this.falloDeAlta.set(null);
  }

  cancelarFormulario(): void {
    this.anotando.set(false);
    this.importe = null;
    this.dia = '';
    this.notas = '';
    this.falloDeAlta.set(null);
  }

  async anotar(): Promise<void> {
    if (this.guardando()) {
      return;
    }

    this.falloDeAlta.set(null);

    if (!this.importe || this.importe <= 0) {
      this.falloDeAlta.set('movimiento.importeObligatorio');
      return;
    }

    this.guardando.set(true);

    try {
      await this.api.anotarMovimiento(this.id(), {
        tipo: this.tipo,
        importe: this.importe,
        moneda: this.moneda.trim().toUpperCase() || 'CAD',
        dia: this.dia || null,
        notas: this.notas.trim() || null,
      });

      this.cancelarFormulario();
      await this.cargar();
    } catch (error: unknown) {
      this.falloDeAlta.set(this.claveDe(error));
    } finally {
      this.guardando.set(false);
    }
  }

  private claveDe(error: unknown): string {
    return error instanceof HttpErrorResponse
      ? (codigoDe(error) ?? 'comun.error')
      : 'comun.error';
  }
}
