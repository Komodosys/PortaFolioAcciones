import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { codigoDe } from '../../../core/api/interceptores';
import { HttpErrorResponse } from '@angular/common/http';
import { Sesion } from '../../../core/sesion/sesion';
import { CarterasApi } from '../carteras.api';
import { ResumenDeCartera } from '../modelos';
import { TarjetaDeCartera } from '../tarjeta/tarjeta-de-cartera';

/** Las carteras de una persona, para poder agruparlas. */
interface Grupo {
  readonly propietario: string;
  readonly carteras: readonly ResumenDeCartera[];
}

/**
 * La lista de carteras: la pantalla donde se entra.
 *
 * Se enseñan **archivadas incluidas**, en su propia seccion, porque archivar es esconder y no borrar: si
 * no aparecieran en ningun sitio, archivar una cartera por error pareceria haberla perdido.
 *
 * Y se separan **las mias de las de las demas**. El administrador ve todas en la misma lista, y sin
 * separarlas no solo no sabria cuales son suyas: podria abrir la de otra persona **sin enterarse**.
 */
@Component({
  selector: 'app-lista-de-carteras',
  imports: [FormsModule, TranslocoDirective, TarjetaDeCartera],
  templateUrl: './lista.html',
  // El estilo vive en la plantilla, con clases de Tailwind: no queda SCSS que mantener.
  host: { class: 'block' },
})
export class ListaDeCarteras {
  private readonly api = inject(CarterasApi);
  private readonly sesion = inject(Sesion);

  readonly carteras = signal<ResumenDeCartera[]>([]);
  readonly cargando = signal(true);
  readonly fallo = signal<string | null>(null);

  /** Si el formulario de nueva cartera esta desplegado. */
  readonly creando = signal(false);
  readonly guardando = signal(false);

  nuevaCartera = '';
  nuevaMoneda = '';

  /**
   * El tipo de registro elegido.
   *
   * **Arranca vacío a propósito**, y el formulario no deja guardar sin elegirlo. Poner «no registrado» por
   * defecto sería la etiqueta equivocada en una cuenta de jubilación, y nadie la revisaría.
   */
  nuevoTipo = '';

  /** Las tres opciones. Las claves van escritas enteras para que el comprobador de idiomas las vea. */
  readonly tipos = [
    { valor: 'RegisteredRetirement', clave: 'cartera.tipos.RegisteredRetirement' },
    { valor: 'RegisteredSavings', clave: 'cartera.tipos.RegisteredSavings' },
    { valor: 'NotRegistered', clave: 'cartera.tipos.NotRegistered' },
  ];

  /** El error del formulario, como codigo de la API. */
  readonly falloDeAlta = signal<string | null>(null);

  /** Las mias, en uso. */
  readonly mias = computed(() => this.deQuien(true).filter((cartera) => !cartera.isArchived));

  /** Las mias archivadas. */
  readonly misArchivadas = computed(() => this.deQuien(true).filter((cartera) => cartera.isArchived));

  /**
   * Las de las demas, agrupadas por persona.
   *
   * Se agrupan en vez de mezclarse: saber que hay 17 carteras ajenas no sirve de nada; saber **de quien**
   * son, si.
   */
  readonly deOtras = computed<readonly Grupo[]>(() => {
    const ajenas = this.deQuien(false);

    const porPropietario = new Map<string, ResumenDeCartera[]>();

    for (const cartera of ajenas) {
      const lista = porPropietario.get(cartera.ownerDisplayName) ?? [];
      lista.push(cartera);
      porPropietario.set(cartera.ownerDisplayName, lista);
    }

    return [...porPropietario.entries()]
      .map(([propietario, carteras]) => ({ propietario, carteras }))
      .sort((a, b) => a.propietario.localeCompare(b.propietario));
  });

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.fallo.set(null);

    try {
      // Se piden tambien las archivadas: la pantalla las separa, no las esconde.
      const todas = await this.api.listar(true);
      this.carteras.set(todas);
    } catch {
      this.fallo.set('comun.error');
    } finally {
      this.cargando.set(false);
    }
  }

  /**
   * Separa las mias de las de las demas.
   *
   * La comparacion es por **identificador de persona** y no por nombre: dos personas de la casa pueden
   * llamarse parecido, y el nombre se puede cambiar.
   */
  private deQuien(mias: boolean): ResumenDeCartera[] {
    const yo = this.sesion.usuario()?.id;

    return this.carteras().filter((cartera) => (cartera.ownerUserId === yo) === mias);
  }

  abrirFormulario(): void {
    this.creando.set(true);
    this.falloDeAlta.set(null);
  }

  cancelarFormulario(): void {
    this.creando.set(false);
    this.nuevaCartera = '';
    this.nuevaMoneda = '';
    this.nuevoTipo = '';
    this.falloDeAlta.set(null);
  }

  async crear(): Promise<void> {
    if (this.guardando()) {
      return;
    }

    this.falloDeAlta.set(null);

    if (!this.nuevaCartera.trim() || !this.nuevoTipo) {
      this.falloDeAlta.set('cartera.nombreObligatorio');
      return;
    }

    this.guardando.set(true);

    try {
      // Moneda vacia significa «heredar la de mi perfil», y por eso se manda nula y no una cadena vacia.
      await this.api.crear(this.nuevaCartera.trim(), this.nuevoTipo, this.nuevaMoneda.trim() || null);
      this.cancelarFormulario();
      await this.cargar();
    } catch (error: unknown) {
      this.falloDeAlta.set(
        error instanceof HttpErrorResponse ? (codigoDe(error) ?? 'comun.error') : 'comun.error',
      );
    } finally {
      this.guardando.set(false);
    }
  }

  // ------------------------------------------------------------------------------------------------
  // Las tres acciones de la tarjeta
  // ------------------------------------------------------------------------------------------------

  /** La cartera que se está editando, o nula si el diálogo está cerrado. */
  readonly editando = signal<ResumenDeCartera | null>(null);
  readonly guardandoEdicion = signal(false);
  readonly falloDeEdicion = signal<string | null>(null);

  /** Lo que se está escribiendo en el diálogo de edición. */
  editarNombre = '';
  editarTipo = '';
  editarMoneda = '';

  /** La cartera que se quiere borrar, o nula. */
  readonly borrando = signal<ResumenDeCartera | null>(null);
  readonly borrandoEnCurso = signal(false);

  /** La cartera cuya acción está en marcha, para deshabilitar su menú y no pulsar dos veces. */
  readonly ocupada = signal<string | null>(null);

  abrirEdicion(cartera: ResumenDeCartera): void {
    this.editarNombre = cartera.name;
    this.editarTipo = cartera.registrationType;
    this.editarMoneda = cartera.inheritsBaseCurrency ? '' : cartera.baseCurrency;
    this.falloDeEdicion.set(null);
    this.editando.set(cartera);
  }

  cancelarEdicion(): void {
    this.editando.set(null);
    this.falloDeEdicion.set(null);
  }

  async guardarEdicion(): Promise<void> {
    const cartera = this.editando();

    if (!cartera || this.guardandoEdicion()) {
      return;
    }

    if (!this.editarNombre.trim() || !this.editarTipo) {
      this.falloDeEdicion.set('cartera.nombreObligatorio');
      return;
    }

    this.guardandoEdicion.set(true);
    this.falloDeEdicion.set(null);

    try {
      // Una sola llamada: nombre, tipo y moneda a la vez. Con tres, una podría fallar y dejar la cartera a
      // medio editar, y el libro de auditoría tendría tres entradas para un solo cambio.
      await this.api.editar(
        cartera.id,
        this.editarNombre.trim(),
        this.editarTipo,
        this.editarMoneda.trim() || null,
      );

      this.editando.set(null);
      await this.cargar();
    } catch (error: unknown) {
      this.falloDeEdicion.set(this.claveDe(error));
    } finally {
      this.guardandoEdicion.set(false);
    }
  }

  abrirBorrado(cartera: ResumenDeCartera): void {
    this.borrando.set(cartera);
  }

  cancelarBorrado(): void {
    this.borrando.set(null);
  }

  async confirmarBorrado(): Promise<void> {
    const cartera = this.borrando();

    if (!cartera || this.borrandoEnCurso()) {
      return;
    }

    this.borrandoEnCurso.set(true);

    try {
      await this.api.borrar(cartera.id);
      this.borrando.set(null);
      await this.cargar();
    } catch (error: unknown) {
      // Lo normal aquí es 409: la cartera tiene posiciones. El mensaje lo pone la traducción del código
      // `portfolio.has_positions`, así que se cierra el diálogo y se enseña el motivo arriba.
      this.borrando.set(null);
      this.fallo.set(this.claveDe(error));
    } finally {
      this.borrandoEnCurso.set(false);
    }
  }

  /** Archiva o desarchiva, según cómo esté. */
  async alternarArchivo(cartera: ResumenDeCartera): Promise<void> {
    if (this.ocupada()) {
      return;
    }

    this.ocupada.set(cartera.id);

    try {
      if (cartera.isArchived) {
        await this.api.desarchivar(cartera.id);
      } else {
        await this.api.archivar(cartera.id);
      }

      await this.cargar();
    } catch (error: unknown) {
      this.fallo.set(this.claveDe(error));
    } finally {
      this.ocupada.set(null);
    }
  }

  /** El código de error de la API, o el genérico. */
  private claveDe(error: unknown): string {
    return error instanceof HttpErrorResponse ? (codigoDe(error) ?? 'comun.error') : 'comun.error';
  }
}
