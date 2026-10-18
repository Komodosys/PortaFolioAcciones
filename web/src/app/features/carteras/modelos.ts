/**
 * Una cartera, tal como la devuelve `GET /api/portfolios`.
 *
 * Los nombres son los del contrato de la API y **no se traducen**: son identificadores, no textos.
 */
export interface ResumenDeCartera {
  readonly id: string;
  readonly name: string;

  /** La moneda en la que se presentan sus totales, ya resuelta por la API. */
  readonly baseCurrency: string;

  /**
   * Si esa moneda la hereda de la persona en vez de tenerla propia.
   *
   * Se dice porque no es lo mismo: si la hereda, cambiarla en el perfil cambia tambien la de esta
   * cartera, y quien la mire tiene derecho a saberlo sin abrir la configuracion.
   */
  readonly inheritsBaseCurrency: boolean;

  /**
   * Como esta registrada a efectos fiscales: `RegisteredRetirement`, `RegisteredSavings` o `NotRegistered`.
   *
   * Viaja como el **nombre del valor**, no traducido y no como numero: el idioma lo pone la pantalla. Y no
   * se traduce en la API porque el servidor no sabe en que idioma esta mirando cada persona — y ademas la
   * misma cosa se llama REER en un comprobante de Revenu Quebec y RRSP en uno de la CRA.
   */
  readonly registrationType: string;

  readonly isArchived: boolean;
  readonly sortOrder: number;

  /**
   * Cuantas posiciones tiene.
   *
   * Esta a la vista porque es lo que decide **si se puede borrar**: una cartera con posiciones se archiva,
   * no se borra. Sin este dato la pantalla tendria que enseñar un boton que va a fallar.
   */
  readonly positionCount: number;

  /**
   * De quien es la cartera.
   *
   * Existe porque **el administrador ve las carteras de todas las personas en la misma lista**. Sin esto,
   * no solo es confuso —«¿cuales son las mias?»—, sino que puede abrir y modificar la cartera de otra
   * persona **sin saberlo**.
   */
  readonly ownerUserId: string;
  readonly ownerDisplayName: string;
}

/**
 * Una posicion dentro de la vista de cartera.
 *
 * Se reciben **los dos importes**, el de la moneda del instrumento y el convertido a la moneda base, y se
 * enseñan los dos. No es duplicacion: el convertido depende de un tipo de cambio que cambia a diario, asi
 * que el de la moneda original es el unico que sigue significando lo mismo mañana.
 */
export interface PosicionEnVista {
  readonly id: string;
  readonly symbol: string;
  readonly instrumentName: string;
  readonly quantity: number;

  readonly priceAmount: number;
  readonly priceCurrency: string;

  /**
   * De donde salio el precio: `manual`, `quote` o `close`.
   *
   * Se enseña porque no es lo mismo un precio que puso una persona que uno de mercado, ni uno de hoy que
   * uno del ultimo cierre.
   */
  readonly priceSource: string;

  readonly priceDate: string | null;

  /**
   * Como esta el mercado donde cotiza: `Open`, `Closed` o `NotATradingDay`.
   *
   * Sin esto, un precio de hace dos dias y uno de hace dos minutos se leen igual.
   */
  readonly marketState: string;

  /**
   * Si el precio es viejo: anterior a la ultima sesion de su bolsa.
   *
   * Va aparte del estado del mercado porque son cosas distintas: el mercado puede estar cerrado y el
   * precio ser el de hoy, que es lo normal.
   */
  readonly isStale: boolean;

  /**
   * El cierre de la sesion anterior, si se conoce.
   *
   * Nulo cuando no: **no se supone** que el precio de ayer fuera el de hoy. Una compra de hoy no tiene
   * «ayer», y un fondo que publica cada dos dias tampoco.
   */
  readonly previousClose: number | null;

  /** Lo que ha cambiado hoy, en la moneda del precio. **Nulo no es cero**: es que no se sabe. */
  readonly dailyChange: number | null;

  /** Lo mismo, en la moneda base. */
  readonly baseDailyChange: number | null;

  /** Lo que ha cambiado hoy, en tanto por ciento sobre el valor de ayer. */
  readonly dailyReturn: number | null;

  readonly marketValue: number;
  readonly cost: number;

  readonly baseMarketValue: number;
  readonly baseCost: number;
  readonly baseGain: number;
  readonly baseReturn: number;
  readonly weight: number | null;

  readonly localGain: number | null;
  readonly localReturn: number | null;
}

/**
 * Un desdoblamiento que una posicion **todavia no ha aplicado**.
 *
 * Existe porque un desdoblamiento sin aplicar **da una cifra falsa**, y de las que asustan: con un 5:1 la
 * cantidad se queda como estaba y el precio se divide, asi que la cartera se lee como si hubiera perdido
 * el 80 % de un dia para otro.
 */
export interface DesdoblamientoPendiente {
  readonly positionId: string;
  readonly symbol: string;
  readonly date: string;
  readonly ratio: number;
  readonly quantity: number;
}

/** Una moneda para la que la API no ha podido conseguir tipo de cambio. */
export interface MonedaSinTipo {
  readonly currency: string;
  readonly positions: number;
}

/**
 * Una posicion que **no se ha podido valorar**, con lo que si se sabe de ella.
 *
 * Se recibe aparte de las valoradas y no como campos nulos dentro de ellas: asi, cuando se enseña una
 * posicion valorada, sus importes **siempre** tienen sentido y no hay que comprobar si son nulos. Ese
 * olvido seria una resta que sale mal.
 *
 * Existe porque **no enseñarlas seria peor**: quien acaba de dar de alta una posicion y no la ve en la
 * tabla piensa que no se ha guardado.
 */
export interface PosicionSinValorar {
  readonly id: string;
  readonly symbol: string;
  readonly instrumentName: string;
  readonly quantity: number;
  readonly cost: number;
  readonly costCurrency: string;

  /** Por que no se ha valorado: `sin-precio` o `sin-tipo-de-cambio`. */
  readonly reason: string;
}

/**
 * La vista de una cartera, **calculada en el servidor**.
 *
 * El cliente no suma nada: recibe los totales hechos. Si los calculara, dos pantallas podrian dar cifras
 * distintas de lo mismo, y no habria forma de saber cual es la buena.
 */
export interface VistaDeCartera {
  readonly portfolioId: string;
  readonly name: string;
  readonly baseCurrency: string;

  /**
   * De quien es.
   *
   * Va en la vista y no solo en el listado porque el aviso de «estas tocando datos ajenos» tiene que estar
   * **en la pantalla donde se tocan**: quien entra directo por un enlace no pasa por el listado.
   */
  readonly ownerUserId: string;
  readonly ownerDisplayName: string;

  readonly totalValue: number;
  readonly totalCost: number;
  readonly totalGain: number;
  readonly totalReturn: number | null;

  /**
   * Lo que ha cambiado la cartera **hoy**, en moneda base.
   *
   * Si `isDailyComplete` es falso, es **solo la parte que se sabe**.
   */
  readonly totalDailyChange: number;

  /**
   * Lo que ha cambiado hoy, en tanto por ciento.
   *
   * **Nulo si falta algun cambio del dia**, porque el porcentaje se calcula sobre el valor de ayer y ese
   * valor tambien estaria mal.
   */
  readonly totalDailyReturn: number | null;

  /**
   * Si se conoce el cambio del dia de **todas** las posiciones valoradas.
   *
   * Es falso en cuanto a una sola le falte el cierre anterior, y entonces el total del dia queda a medias
   * y hay que decirlo.
   */
  readonly isDailyComplete: boolean;

  /**
   * Si **todas** las posiciones se han podido valorar.
   *
   * Cuando es falso, los totales son **parciales** y hay que decirlo. Un total incompleto presentado como
   * total es una cifra falsa.
   */
  readonly isComplete: boolean;

  readonly closedPositions: number;
  readonly positions: readonly PosicionEnVista[];

  /** Posiciones sin ningun precio utilizable, por su identificador. */
  readonly positionsWithoutPrice: readonly string[];

  /** Las que no se han podido valorar, con sus datos, para poder enseñarlas igualmente. */
  readonly unpricedPositions: readonly PosicionSinValorar[];

  readonly missingCurrencies: readonly MonedaSinTipo[];

  /** Desdoblamientos que alguna posicion no ha aplicado. */
  readonly pendingSplits: readonly DesdoblamientoPendiente[];

  /** La fecha de los tipos de cambio usados, o nula. */
  readonly ratesDate: string | null;
}
