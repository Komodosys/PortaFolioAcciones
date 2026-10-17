import { PosicionEnVista } from './modelos';

/** Una columna por la que se puede ordenar la tabla de posiciones. */
export type Columna =
  | 'simbolo'
  | 'nombre'
  | 'cantidad'
  | 'coste'
  | 'precio'
  | 'valor'
  | 'hoy'
  | 'ganancia'
  | 'peso';

/** Por que columna se ordena y en que sentido. */
export interface Orden {
  readonly campo: Columna;
  readonly descendente: boolean;
}

/**
 * Compara dos posiciones por una columna.
 *
 * **Se comparan los números en crudo, nunca el texto ya formateado.** Y esa es toda la razón de que esto
 * viva en su propio fichero con sus pruebas: ordenar `1.234,56` como texto pone `9,00` **después** de
 * `1.234,00`, porque compara letra a letra. Es un error que no se ve con cuatro filas y que aparece justo
 * cuando la cartera crece, que es cuando nadie lo está mirando.
 *
 * Los textos se comparan con las reglas del idioma, para que la Ñ y los acentos caigan donde tienen que
 * caer y no al final del alfabeto.
 */
export function compararPosiciones(
  a: PosicionEnVista,
  b: PosicionEnVista,
  campo: Columna,
  idioma: string,
): number {
  switch (campo) {
    case 'simbolo':
      return a.symbol.localeCompare(b.symbol, idioma);

    case 'nombre':
      return a.instrumentName.localeCompare(b.instrumentName, idioma);

    case 'cantidad':
      return a.quantity - b.quantity;

    case 'coste':
      return a.cost - b.cost;

    case 'precio':
      return a.priceAmount - b.priceAmount;

    case 'valor':
      return a.marketValue - b.marketValue;

    case 'hoy':
      // Una posicion sin cambio del dia cuenta como cero para ordenar: no se sabe cuanto ha cambiado, y
      // mandarla al fondo por eso la sacaria de su sitio sin motivo.
      return (a.baseDailyChange ?? 0) - (b.baseDailyChange ?? 0);

    case 'ganancia':
      return a.baseGain - b.baseGain;

    case 'peso':
      // Una posición sin peso —la única de la cartera— cuenta como cero: no está en ningún sitio, y
      // mandarla al final por ser nula la sacaría de su sitio sin motivo.
      return (a.weight ?? 0) - (b.weight ?? 0);

    default:
      return 0;
  }
}

/**
 * El sentido con el que se ordena al pulsar una columna por primera vez.
 *
 * Los números empiezan de **mayor a menor** —lo que más pesa primero, que es lo que se quiere ver— y los
 * textos de la A a la Z. Empezar los textos de la Z a la A sería no entender para qué se pulsa una
 * columna de nombres.
 */
export function sentidoInicial(campo: Columna): boolean {
  return campo !== 'simbolo' && campo !== 'nombre';
}

/** El orden que resulta de volver a pulsar una columna. */
export function ordenarPor(actual: Orden, campo: Columna): Orden {
  return actual.campo === campo
    ? { campo, descendente: !actual.descendente }
    : { campo, descendente: sentidoInicial(campo) };
}
