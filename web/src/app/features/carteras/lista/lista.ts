import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { codigoDe } from '../../../core/api/interceptores';
import { HttpErrorResponse } from '@angular/common/http';
import { Sesion } from '../../../core/sesion/sesion';
import { CarterasApi } from '../carteras.api';
import { ResumenDeCartera } from '../modelos';

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
  imports: [FormsModule, RouterLink, TranslocoDirective],
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
}
