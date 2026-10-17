/**
 * Un instrumento del catalogo de la casa.
 *
 * Los nombres son los del contrato de la API.
 */
export interface Instrumento {
  readonly id: string;
  readonly symbol: string;
  readonly name: string;
  readonly assetType: string;
  readonly quoteCurrency: string;
  readonly exchange: string;
  readonly isin: string | null;
  readonly valuationMode: string;
  readonly pricingFrequency: string;

  /** Con que nombre lo conoce la fuente de precios. */
  readonly providerKey: string | null;

  /** Con que codigo lo conoce el broker. */
  readonly brokerCode: string | null;

  readonly isActive: boolean;

  /**
   * Los desdoblamientos que se le conocen.
   *
   * Explican un salto del precio: sin saber que hubo un 5:1, una caida del 80 % de un dia para otro
   * parece una ruina, y con el se entiende que no paso nada.
   */
  readonly splits: readonly { readonly date: string; readonly ratio: number }[];
}

/**
 * Un resultado de buscar **fuera**, en la fuente de mercado.
 *
 * `providerKey` y `symbol` son cosas distintas: para muchos fondos canadienses el nombre en la fuente es
 * un codigo interno (`0P0001K2XD.TO`) que no se parece en nada a lo que se enseña.
 */
export interface ResultadoDeFuente {
  readonly providerKey: string;
  readonly symbol: string;
  readonly name: string;

  /** La bolsa segun la fuente, que **no** es un codigo MIC. */
  readonly exchange: string | null;

  /** La bolsa traducida a MIC, o nula si la fuente usa una que no se reconoce. */
  readonly mic: string | null;

  readonly assetType: string | null;
  readonly currency: string | null;
  readonly source: string;
}

/** Lo que se manda para dar de alta un instrumento. */
export interface AltaDeInstrumento {
  readonly symbol: string;
  readonly name: string;
  readonly assetType: string;
  readonly quoteCurrency: string;
  readonly exchange: string;
  readonly valuationMode: string;
  readonly pricingFrequency: string;
  readonly isin?: string | null;
  readonly country?: string | null;
  readonly providerKey?: string | null;
  readonly brokerCode?: string | null;
}
