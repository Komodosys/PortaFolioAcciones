import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged, from, switchMap, tap } from 'rxjs';
import { codigoDe } from '../../core/api/interceptores';
import { InstrumentosApi } from '../../core/api/instrumentos/instrumentos.api';
import {
  AltaDeInstrumento,
  Instrumento,
  ResultadoDeFuente,
} from '../../core/api/instrumentos/modelos';

/** Una opcion de un desplegable. */
interface Opcion {
  readonly valor: string;
  readonly clave: string;
}

/**
 * Lo que se esta editando antes de dar de alta.
 *
 * Es **mutable** porque es un formulario: cada campo se cambia por separado. Al confirmar se convierte en
 * el objeto inmutable que se manda a la API.
 */
interface AltaEnEdicion {
  symbol: string;
  name: string;
  assetType: string;
  quoteCurrency: string;
  exchange: string;
  valuationMode: string;
  pricingFrequency: string;
  providerKey: string | null;
  brokerCode: string;
  isin: string;
}

/**
 * Busca un instrumento y lo elige.
 *
 * **Hace dos busquedas distintas y no las mezcla**, porque son preguntas distintas:
 * <list type="bullet">
 *   <item>La del **catalogo** de la casa —«¿ya tengo esto?»—, que es instantanea y se hace sola mientras se
 *   escribe.</item>
 *   <item>La de **fuera** —«¿cómo se llama esto ahí fuera?»—, que hay que pedir a proposito porque depende
 *   de un servicio ajeno y puede tardar.</item>
 * </list>
 *
 * Y **no da de alta nada sin confirmarlo**: cuando se elige un resultado de fuera, enseña los datos que se
 * van a guardar —incluido el codigo del broker— y espera. Esa confirmacion es el punto donde una persona
 * comprueba que el instrumento es el que dice su estado de cuenta, y es lo unico que resuelve el caso de
 * los fondos canadienses de Fundserv: **su codigo no existe en la fuente de precios**, asi que la
 * equivalencia hay que emparejarla a mano una vez por instrumento.
 */
@Component({
  selector: 'app-buscador-instrumento',
  imports: [FormsModule, TranslocoDirective],
  templateUrl: './buscador-instrumento.html',
  // El estilo vive en la plantilla, con clases de Tailwind: no queda SCSS que mantener.
  host: { class: 'block' },
})
export class BuscadorInstrumento {
  private readonly api = inject(InstrumentosApi);

  /** El instrumento elegido y confirmado. */
  readonly elegido = output<Instrumento>();

  /** Lo que se esta escribiendo. Es una señal porque la busqueda reacciona a ella. */
  readonly texto = signal('');

  readonly resultados = signal<Instrumento[]>([]);
  readonly resultadosFuera = signal<ResultadoDeFuente[]>([]);
  readonly buscando = signal(false);
  readonly buscandoFuera = signal(false);
  readonly fallo = signal<string | null>(null);

  /** Lo que se esta confirmando antes de darlo de alta, o nulo si no hay nada que confirmar. */
  readonly porConfirmar = signal<AltaEnEdicion | null>(null);
  readonly confirmando = signal(false);

  readonly tipos: readonly Opcion[] = [
    { valor: 'Equity', clave: 'instrumento.tipo.Equity' },
    { valor: 'Etf', clave: 'instrumento.tipo.Etf' },
    { valor: 'MutualFund', clave: 'instrumento.tipo.MutualFund' },
    { valor: 'Bond', clave: 'instrumento.tipo.Bond' },
    { valor: 'BondFund', clave: 'instrumento.tipo.BondFund' },
    { valor: 'Index', clave: 'instrumento.tipo.Index' },
  ];

  readonly modosDeValoracion: readonly Opcion[] = [
    { valor: 'Market', clave: 'instrumento.modo.Market' },
    { valor: 'Nav', clave: 'instrumento.modo.Nav' },
    { valor: 'Manual', clave: 'instrumento.modo.Manual' },
  ];

  readonly frecuencias: readonly Opcion[] = [
    { valor: 'IntradayDelayed', clave: 'instrumento.frecuencia.IntradayDelayed' },
    { valor: 'EndOfDay', clave: 'instrumento.frecuencia.EndOfDay' },
    { valor: 'DailyNav', clave: 'instrumento.frecuencia.DailyNav' },
    { valor: 'Manual', clave: 'instrumento.frecuencia.Manual' },
  ];

  constructor() {
    // La busqueda del catalogo se hace sola mientras se escribe, con un retardo: sin el, cada tecla
    // lanzaria una peticion y escribir «Veeva» serian cinco.
    toObservable(this.texto)
      .pipe(
        debounceTime(250),
        distinctUntilChanged(),
        tap(() => {
          this.buscando.set(true);
          this.fallo.set(null);
        }),
        switchMap((texto) => from(this.buscarEnCatalogo(texto))),
        takeUntilDestroyed(),
      )
      .subscribe((encontrados) => {
        this.resultados.set(encontrados);
        this.buscando.set(false);
      });
  }

  /** Cambia un campo del formulario de alta. */
  actualizar(campo: keyof AltaEnEdicion, valor: string): void {
    this.porConfirmar.update((actual) => (actual ? { ...actual, [campo]: valor } : actual));
  }

  /** Busca en la fuente de mercado. Se pide a proposito, no mientras se escribe. */
  async buscarFuera(): Promise<void> {
    const texto = this.texto().trim();

    if (texto.length < 2 || this.buscandoFuera()) {
      return;
    }

    this.buscandoFuera.set(true);
    this.fallo.set(null);
    this.resultadosFuera.set([]);

    try {
      this.resultadosFuera.set(await this.api.buscarFuera(texto));
    } catch {
      this.fallo.set('instrumento.falloAlBuscar');
    } finally {
      this.buscandoFuera.set(false);
    }
  }

  /** Se elige uno del catalogo: no hay nada que confirmar, ya existe. */
  elegirDelCatalogo(instrumento: Instrumento): void {
    this.fallo.set(null);
    this.elegido.emit(instrumento);
  }

  /**
   * Se elige un resultado de fuera: se prepara el alta con lo que dice la fuente y **se pide confirmacion**.
   *
   * Los valores que la fuente no da se rellenan con lo mas probable, y **se enseñan** para que se puedan
   * corregir. Un fondo se valora por su valor liquidativo y se publica una vez al dia; lo demas se valora a
   * mercado. Pero se enseña, no se decide en silencio.
   */
  prepararAlta(resultado: ResultadoDeFuente): void {
    const esFondo = resultado.assetType === 'MutualFund' || resultado.assetType === 'BondFund';

    this.fallo.set(null);
    this.porConfirmar.set({
      symbol: resultado.symbol,
      name: resultado.name,
      assetType: resultado.assetType ?? 'Equity',
      quoteCurrency: resultado.currency ?? 'CAD',
      exchange: resultado.mic ?? '',
      valuationMode: esFondo ? 'Nav' : 'Market',
      pricingFrequency: esFondo ? 'DailyNav' : 'IntradayDelayed',
      providerKey: resultado.providerKey,
      brokerCode: '',
      isin: '',
    });
  }

  cancelarAlta(): void {
    this.porConfirmar.set(null);
    this.fallo.set(null);
  }

  async confirmarAlta(): Promise<void> {
    const datos = this.porConfirmar();

    if (!datos || this.confirmando()) {
      return;
    }

    // La bolsa en codigo MIC es parte de la identidad del instrumento: sin ella, el mismo valor podria
    // acabar dado de alta dos veces.
    if (!datos.exchange.trim()) {
      this.fallo.set('instrumento.faltaBolsa');
      return;
    }

    if (!datos.symbol.trim() || !datos.name.trim()) {
      this.fallo.set('instrumento.faltanDatos');
      return;
    }

    this.confirmando.set(true);
    this.fallo.set(null);

    try {
      const alta: AltaDeInstrumento = {
        symbol: datos.symbol.trim(),
        name: datos.name.trim(),
        assetType: datos.assetType,
        quoteCurrency: datos.quoteCurrency.trim().toUpperCase(),
        exchange: datos.exchange.trim().toUpperCase(),
        valuationMode: datos.valuationMode,
        pricingFrequency: datos.pricingFrequency,
        providerKey: datos.providerKey,
        // Se limpian los vacios: mandar cadena vacia donde se espera nulo obliga a la API a adivinar si es
        // «no lo se» o «lo dejo en blanco».
        brokerCode: datos.brokerCode.trim() || null,
        isin: datos.isin.trim() || null,
      };

      const creado = await this.api.crear(alta);

      this.porConfirmar.set(null);
      this.elegido.emit(creado);
    } catch (error: unknown) {
      this.fallo.set(
        error instanceof HttpErrorResponse ? (codigoDe(error) ?? 'comun.error') : 'comun.error',
      );
    } finally {
      this.confirmando.set(false);
    }
  }

  private async buscarEnCatalogo(texto: string): Promise<Instrumento[]> {
    try {
      return await this.api.buscar(texto.trim());
    } catch {
      // Un fallo buscando en el catalogo no debe romper el formulario: se dice y se sigue.
      this.fallo.set('comun.error');
      return [];
    }
  }
}
