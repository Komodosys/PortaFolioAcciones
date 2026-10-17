/**
 * Formato de numeros para presentar.
 *
 * **Aqui no se calcula nada.** Estas funciones reciben un numero que ya viene calculado de la API y lo
 * escriben como se escribe en cada idioma. Es la regla del proyecto: si el calculo se duplicara en el
 * cliente, dos pantallas podrian dar cifras distintas de lo mismo.
 *
 * Los numeros viajan **en crudo**: se recibe `1234.56`, nunca una cantidad ya formateada. Y no es un
 * capricho: en los estados de cuenta el mismo numero se escribe `1,234.56` en ingles y `1 234,56` en
 * frances, y reinterpretar una cadena ya formateada es justo el error que se quiere evitar.
 */

/**
 * Escribe un importe con su moneda.
 *
 * La moneda se muestra **como codigo** (`CAD 1.234,56`) y no como simbolo, y es una decision que importa:
 * en una cartera con dolares canadienses y dolares estadounidenses, los dos son «$». Con simbolos, dos
 * columnas de la misma tabla pareceran la misma moneda y la mas cara sera la equivocada.
 */
export function formatearImporte(valor: number, moneda: string, idioma: string): string {
  return new Intl.NumberFormat(idioma, {
    style: 'currency',
    currency: moneda,
    currencyDisplay: 'code',
  }).format(valor);
}

/**
 * Escribe una cantidad de unidades.
 *
 * Se recorta a lo que de verdad hace falta: una posicion puede tener `23,00000000` unidades y escribir
 * ocho ceros no informa de nada. Se deja hasta cuatro decimales y se quitan los que sobren, porque hay
 * fondos que si los usan.
 */
export function formatearCantidad(valor: number, idioma: string): string {
  return new Intl.NumberFormat(idioma, {
    minimumFractionDigits: 0,
    maximumFractionDigits: 4,
  }).format(valor);
}

/**
 * Escribe una rentabilidad.
 *
 * La API manda la **proporcion** (`0.3448`), no el tanto por ciento, y aqui se escribe como porcentaje:
 * `Intl` multiplica por cien. Multiplicar a mano seria calcular, y ademas con el riesgo de equivocarse en
 * el redondeo.
 */
export function formatearPorcentaje(proporcion: number, idioma: string): string {
  return new Intl.NumberFormat(idioma, {
    style: 'percent',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(proporcion);
}

/**
 * Escribe un peso sobre el total.
 *
 * El peso es informativo y mas pequeño que la rentabilidad, asi que con un decimal basta: `92,0 %` dice
 * lo mismo que `92,00 %` y ocupa menos.
 */
export function formatearPeso(proporcion: number, idioma: string): string {
  return new Intl.NumberFormat(idioma, {
    style: 'percent',
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  }).format(proporcion);
}

/**
 * Escribe una fecha del calendario.
 *
 * Se recibe `2026-08-31` y se escribe como se escribe en cada idioma. **Se construye desde las partes y
 * no con `new Date('2026-08-31')`**, y eso no es un detalle: esa forma se interpreta como medianoche UTC, y
 * en America eso es el dia anterior. Una fecha de un estado de cuenta no puede aparecer corrida un dia.
 */
export function formatearFecha(iso: string, idioma: string): string {
  const partes = iso.split('-').map(Number);

  if (partes.length !== 3 || partes.some(Number.isNaN)) {
    return iso;
  }

  const [anio, mes, dia] = partes;

  return new Intl.DateTimeFormat(idioma, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(anio, mes - 1, dia));
}
