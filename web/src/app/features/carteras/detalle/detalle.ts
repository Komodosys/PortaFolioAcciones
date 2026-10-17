import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { codigoDe } from '../../../core/api/interceptores';
import { Idioma } from '../../../core/i18n/idioma';
import {
  formatearCantidad,
  formatearFecha,
  formatearImporte,
  formatearPorcentaje,
  formatearPeso,
} from '../../../shared/formato/numeros';
import { CarterasApi } from '../carteras.api';
import { DesdoblamientoPendiente, PosicionEnVista, PosicionSinValorar, VistaDeCartera } from '../modelos';
import { Columna, compararPosiciones, ordenarPor as siguienteOrden } from '../orden';
import { Sesion } from '../../../core/sesion/sesion';
import { PosicionesApi } from '../../../core/api/posiciones/posiciones.api';
import { AvisoDatosAjenos } from '../../../shared/aviso-datos-ajenos/aviso-datos-ajenos';

/** Una fila de la tabla, con sus textos ya formateados. */
interface Fila {
  readonly id: string;
  readonly simbolo: string;
  readonly nombre: string;
  readonly cantidad: string;
  readonly moneda: string;
  readonly coste: string;

  /** Nulo cuando la posicion no se ha podido valorar: se enseña un guion. */
  readonly precio: string | null;
  readonly valor: string | null;
  readonly ganancia: string | null;
  readonly gananciaPorcentaje: string | null;
  readonly peso: string | null;

  /** Para pintar la ganancia en verde o en rojo. */
  readonly signo: 'positiva' | 'negativa' | 'neutra';

  /** Aviso sobre el origen del precio, si hay algo que decir. */
  readonly origenDelPrecio: string | null;

  /** Fecha del precio, formateada. */
  readonly fechaDelPrecio: string | null;

  /** Si el precio es viejo: anterior a la ultima sesion de su bolsa. */
  readonly esViejo: boolean;

  /**
   * Lo que ha cambiado hoy, formateado.
   *
   * Nulo cuando no se sabe el cierre anterior. Entonces **no se enseña un cero**: se enseña un guion, que
   * es lo que significa.
   */
  readonly hoy: string | null;

  /** El porcentaje del dia, o nulo si no se sabe. */
  readonly hoyPorcentaje: string | null;

  /** Para pintar el cambio del dia en verde o en rojo. */
  readonly signoDeHoy: 'positiva' | 'negativa' | 'neutra';

  /**
   * Por que no se ha podido valorar, si es el caso.
   *
   * Se dice en la propia fila y no solo en el aviso de arriba: quien mira una posicion con guiones quiere
   * saber por que los tiene **ahi mismo**.
   */
  readonly sinValorar: string | null;
}

/** Un total, con su texto ya formateado. */
interface Totales {
  readonly valor: string;
  readonly coste: string;
  readonly ganancia: string;
  readonly rentabilidad: string;
  readonly signo: 'positiva' | 'negativa' | 'neutra';

  /** Lo que ha cambiado hoy. */
  readonly hoy: string;
  readonly hoyPorcentaje: string | null;
  readonly signoDeHoy: 'positiva' | 'negativa' | 'neutra';

  /**
   * Si falta el cambio del dia de alguna posicion.
   *
   * Cuando es cierto, el total de hoy es **solo la parte que se sabe** y la pantalla tiene que decirlo: una
   * cifra del dia incompleta presentada como el total invita a sacar conclusiones de la jornada.
   */
  readonly hoyParcial: boolean;
}

/**
 * El detalle de una cartera: sus posiciones y sus totales.
 *
 * **Aqui no se calcula nada financiero.** Los importes llegan ya sumados y convertidos de la API; esta
 * pantalla los formatea segun el idioma y los presenta. Si sumara, dos pantallas podrian dar cifras
 * distintas de lo mismo y no habria forma de saber cual es la buena.
 *
 * El formateo se rehace cuando cambia el idioma, y eso se consigue con `computed`: los textos dependen de
 * la señal del idioma, asi que cambiar de idioma los recalcula solo, sin recargar los datos.
 */
@Component({
  selector: 'app-detalle-de-cartera',
  imports: [RouterLink, TranslocoDirective, AvisoDatosAjenos],
  templateUrl: './detalle.html',
  // El estilo vive en la plantilla, con clases de Tailwind: no queda SCSS que mantener.
  host: { class: 'block' },
})
export class DetalleDeCartera {
  private readonly api = inject(CarterasApi);
  private readonly idioma = inject(Idioma);
  private readonly sesion = inject(Sesion);
  private readonly posiciones = inject(PosicionesApi);

  /** El identificador de la cartera, que llega de la direccion. */
  readonly id = input.required<string>();

  readonly vista = signal<VistaDeCartera | null>(null);
  readonly cargando = signal(true);
  readonly fallo = signal<string | null>(null);

  /** Si hay una operacion en curso, para no disparar dos veces. */
  readonly resolviendo = signal(false);

  /**
   * Como esta el mercado de la cartera, dicho en una linea.
   *
   * Se resume de las posiciones y no se pregunta aparte: una cartera puede tener valores en dos bolsas, y
   * lo que importa es si **alguno** de los mercados donde se tiene algo esta abierto.
   */
  readonly estadoDelMercado = computed(() => {
    const posiciones = this.vista()?.positions ?? [];

    if (posiciones.length === 0) {
      return null;
    }

    if (posiciones.some((posicion) => posicion.marketState === 'Open')) {
      return 'cartera.mercadoAbierto';
    }

    // Si TODAS estan en un dia sin negociacion, decirlo asi: «cerrado» y «hoy no se negocia» no son lo
    // mismo, y el segundo explica por que el precio no se mueve.
    return posiciones.every((posicion) => posicion.marketState === 'NotATradingDay')
      ? 'cartera.mercadoCerradoHoy'
      : 'cartera.mercadoCerrado';
  });

  /**
   * Los desdoblamientos pendientes, con sus textos ya formateados.
   *
   * La cantidad resultante se enseña **antes** de aplicar nada: quien decide si aplicar o no necesita ver
   * en que queda su posicion, y ese numero es justo el que no puede calcular de cabeza.
   */
  readonly desdoblamientos = computed(() => {
    const vista = this.vista();

    if (!vista) {
      return [];
    }

    const idioma = this.idioma.activo();

    return vista.pendingSplits.map((split) => ({
      ...split,
      fecha: formatearFecha(split.date, idioma),
      cantidadAntes: formatearCantidad(split.quantity, idioma),
      cantidadDespues: formatearCantidad(split.quantity * split.ratio, idioma),
      proporcion: `${formatearCantidad(split.ratio, idioma)}:1`,
    }));
  });

  /**
   * Resuelve un desdoblamiento: aplicarlo, o anotar que ya esta reflejado.
   *
   * Las dos opciones existen porque las dos son correctas segun de donde vengan los datos, y la aplicacion
   * no puede saber cual es el caso. Elegir por su cuenta cambiaria las cantidades de alguien sin que lo
   * haya pedido, y eso es peor que preguntar.
   */
  async resolverDesdoblamiento(
    split: DesdoblamientoPendiente,
    aplicar: boolean,
  ): Promise<void> {
    if (this.resolviendo()) {
      return;
    }

    this.resolviendo.set(true);
    this.fallo.set(null);

    try {
      await this.posiciones.aplicarDesdoblamiento(split.positionId, split.ratio, split.date, aplicar);
      await this.cargar(this.id());
    } catch (error: unknown) {
      this.fallo.set(
        error instanceof HttpErrorResponse ? (codigoDe(error) ?? 'comun.error') : 'comun.error',
      );
    } finally {
      this.resolviendo.set(false);
    }
  }

  constructor() {
    // Al cambiar el identificador se vuelve a cargar. Navegar de una cartera a otra reutiliza este
    // componente, asi que sin esto se quedaria con los datos de la anterior.
    effect(() => {
      void this.cargar(this.id());
    });
  }

  async cargar(id: string): Promise<void> {
    this.cargando.set(true);
    this.fallo.set(null);

    try {
      this.vista.set(await this.api.vista(id));
    } catch {
      this.vista.set(null);
      this.fallo.set('cartera.noEncontrada');
    } finally {
      this.cargando.set(false);
    }
  }

  /** Dice si la cartera que se esta viendo es de otra persona. */
  readonly esAjena = computed(() => {
    const vista = this.vista();
    const yo = this.sesion.usuario()?.id;

    // Sin sesion no se deberia llegar aqui; si se llega, se dice que es ajena. Ante la duda, avisar.
    return vista !== null && (yo === undefined || vista.ownerUserId !== yo);
  });

  /**
   * Los totales, formateados.
   */
  readonly totales = computed<Totales | null>(() => {
    const vista = this.vista();

    if (!vista) {
      return null;
    }

    const idioma = this.idioma.activo();

    return {
      valor: formatearImporte(vista.totalValue, vista.baseCurrency, idioma),
      coste: formatearImporte(vista.totalCost, vista.baseCurrency, idioma),
      ganancia: formatearImporte(vista.totalGain, vista.baseCurrency, idioma),
      rentabilidad:
        vista.totalReturn === null ? '—' : formatearPorcentaje(vista.totalReturn, idioma),
      signo: signoDe(vista.totalGain),

      hoy: formatearImporte(vista.totalDailyChange, vista.baseCurrency, idioma),
      hoyPorcentaje:
        vista.totalDailyReturn === null
          ? null
          : formatearPorcentaje(vista.totalDailyReturn, idioma),
      signoDeHoy: signoDe(vista.totalDailyChange),
      hoyParcial: !vista.isDailyComplete,
    };
  });

  /**
   * Por que columna se ordena, y en que sentido.
   *
   * Arranca por valor de mayor a menor, que es lo que se quiere ver al abrir: **lo que mas pesa en la
   * cartera**. Ordenar por simbolo al entrar seria alfabetico y no diria nada de la cartera.
   */
  readonly orden = signal<{ campo: Columna; descendente: boolean }>({
    campo: 'valor',
    descendente: true,
  });

  /** Las columnas de la tabla, para no repetir ocho veces lo mismo en la plantilla. */
  readonly columnas: readonly { campo: Columna; clave: string; numerica: boolean }[] = [
    { campo: 'simbolo', clave: 'posiciones.simbolo', numerica: false },
    { campo: 'nombre', clave: 'posiciones.nombre', numerica: false },
    { campo: 'cantidad', clave: 'posiciones.cantidad', numerica: true },
    { campo: 'coste', clave: 'posiciones.coste', numerica: true },
    { campo: 'precio', clave: 'posiciones.precio', numerica: true },
    { campo: 'valor', clave: 'posiciones.valor', numerica: true },
    { campo: 'hoy', clave: 'posiciones.hoy', numerica: true },
    { campo: 'ganancia', clave: 'posiciones.ganancia', numerica: true },
    { campo: 'peso', clave: 'posiciones.peso', numerica: true },
  ];

  /**
   * Ordena por una columna. Si ya se ordenaba por ella, cambia el sentido.
   *
   * La cuenta la hace `orden.ts`, que tiene sus propias pruebas: es la parte con trampa —comparar el
   * número y no el texto— y no debe depender de que este componente esté bien.
   */
  ordenarPor(campo: Columna): void {
    this.orden.update((actual) => siguienteOrden(actual, campo));
  }

  /** La flecha que se enseña en la cabecera de la columna que manda. */
  readonly flechaDe = (campo: Columna): string => {
    const actual = this.orden();

    if (actual.campo !== campo) {
      return '';
    }

    return actual.descendente ? '▾' : '▴';
  };

  /**
   * Las filas de la tabla, formateadas: primero las valoradas, y despues las que no se han podido.
   *
   * **Se ordena sobre los numeros en crudo, no sobre el texto ya formateado.** Ordenar `1.234,56` como
   * texto pondria `9,00` despues de `1.234,00`, porque compara letra a letra. Es un error que no se ve
   * con pocas filas y aparece justo cuando la cartera crece.
   */
  readonly filas = computed<readonly Fila[]>(() => {
    const vista = this.vista();

    if (!vista) {
      return [];
    }

    const idioma = this.idioma.activo();
    const { campo, descendente } = this.orden();
    const signo = descendente ? -1 : 1;

    const valoradas = [...vista.positions].sort(
      (a, b) => signo * compararPosiciones(a, b, campo, idioma),
    );

    return [
      ...valoradas.map((posicion) => this.aFila(posicion, idioma)),

      // Las que no se han podido valorar van AL FINAL y con guiones, pero van: si no aparecieran, quien
      // acaba de dar de alta una posicion pensaria que no se ha guardado. Y no se mezclan con las demas
      // aunque se ordene por simbolo: son otro grupo —no tienen valor— y mezclarlas las haria faciles de
      // perder entre las que si lo tienen.
      ...vista.unpricedPositions.map((posicion) => this.aFilaSinValorar(posicion, idioma)),
    ];
  });

  /** Las posiciones que no se han podido valorar, para poder decir cuales son en el aviso. */
  readonly sinPrecio = computed<readonly string[]>(
    () => this.vista()?.unpricedPositions.map((posicion) => posicion.symbol) ?? [],
  );

  private aFila(posicion: PosicionEnVista, idioma: string): Fila {
    return {
      id: posicion.id,
      simbolo: posicion.symbol,
      nombre: posicion.instrumentName,
      cantidad: formatearCantidad(posicion.quantity, idioma),
      moneda: posicion.priceCurrency,
      coste: formatearImporte(posicion.cost, posicion.priceCurrency, idioma),
      precio: formatearImporte(posicion.priceAmount, posicion.priceCurrency, idioma),
      valor: formatearImporte(posicion.marketValue, posicion.priceCurrency, idioma),

      // La ganancia se enseña **en la moneda base**, que es la unica en la que se pueden sumar posiciones
      // de monedas distintas. La de la moneda del instrumento esta en los datos, para quien la quiera.
      ganancia: formatearImporte(posicion.baseGain, this.vista()!.baseCurrency, idioma),
      gananciaPorcentaje: formatearPorcentaje(posicion.baseReturn, idioma),
      peso: posicion.weight === null ? '—' : formatearPeso(posicion.weight, idioma),

      signo: signoDe(posicion.baseGain),
      origenDelPrecio: posicion.priceSource === 'manual' ? 'posiciones.precioManual' : null,
      fechaDelPrecio: posicion.priceDate ? formatearFecha(posicion.priceDate, idioma) : null,
      esViejo: posicion.isStale,

      hoy:
        posicion.baseDailyChange === null
          ? null
          : formatearImporte(posicion.baseDailyChange, this.vista()!.baseCurrency, idioma),
      hoyPorcentaje:
        posicion.dailyReturn === null ? null : formatearPorcentaje(posicion.dailyReturn, idioma),
      signoDeHoy: signoDe(posicion.baseDailyChange ?? 0),

      sinValorar: null,
    };
  }

  /** Una posicion que no se ha podido valorar: se enseña lo que se sabe y guiones donde no hay dato. */
  private aFilaSinValorar(posicion: PosicionSinValorar, idioma: string): Fila {
    return {
      id: posicion.id,
      simbolo: posicion.symbol,
      nombre: posicion.instrumentName,
      cantidad: formatearCantidad(posicion.quantity, idioma),
      moneda: posicion.costCurrency,
      coste: formatearImporte(posicion.cost, posicion.costCurrency, idioma),
      precio: null,
      valor: null,
      ganancia: null,
      gananciaPorcentaje: null,
      peso: null,
      signo: 'neutra',
      origenDelPrecio: null,
      fechaDelPrecio: null,
      // Una posicion sin precio no tiene precio viejo: no tiene ninguno, y eso ya se dice aparte.
      esViejo: false,
      // Y tampoco tiene cambio del dia. Un guion, no un cero.
      hoy: null,
      hoyPorcentaje: null,
      signoDeHoy: 'neutra',
      sinValorar:
        posicion.reason === 'sin-tipo-de-cambio'
          ? 'posiciones.sinTipoDeCambio'
          : 'posiciones.sinPrecio',
    };
  }
}

/** Decide el color de una cifra. El cero no es ni ganancia ni perdida, y no se pinta de ninguno. */
function signoDe(valor: number): 'positiva' | 'negativa' | 'neutra' {
  if (valor > 0) {
    return 'positiva';
  }

  return valor < 0 ? 'negativa' : 'neutra';
}
