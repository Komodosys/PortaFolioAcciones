import {
  formatearCantidad,
  formatearFecha,
  formatearImporte,
  formatearPorcentaje,
  formatearPeso,
} from './numeros';

/**
 * El formato de numeros.
 *
 * Aqui no se calcula nada: se recibe un numero ya calculado por la API y se escribe como se escribe en
 * cada idioma. Lo que se prueba, por tanto, es que **se escribe bien**, y muy en particular las tres
 * trampas que tiene esto.
 */

/** Quita los espacios raros que mete el formateador (el fino y el duro) para poder comparar. */
const limpiar = (texto: string): string => texto.replace(/[\u00a0\u202f\u2009]/g, ' ');

describe('formatearImporte', () => {
  it('escribe la moneda como CODIGO, no como simbolo', () => {
    const texto = limpiar(formatearImporte(1234.56, 'CAD', 'es'));

    // Es la decision que importa: en una cartera con dolares canadienses y estadounidenses, los dos son
    // «$». Con simbolos, dos columnas de la misma tabla parecerian la misma moneda.
    expect(texto).toContain('CAD');
    expect(texto).not.toContain('$');
  });

  it('distingue dos monedas que comparten simbolo', () => {
    const canadienses = formatearImporte(1000, 'CAD', 'en');
    const estadounidenses = formatearImporte(1000, 'USD', 'en');

    expect(canadienses).not.toBe(estadounidenses);
  });

  it('escribe con las convenciones de cada idioma', () => {
    // En ingles, el separador de millares y el punto decimal.
    expect(limpiar(formatearImporte(12345.67, 'CAD', 'en'))).toContain('12,345.67');

    // Y en espanol, al reves: punto para los millares y coma para los decimales.
    expect(limpiar(formatearImporte(12345.67, 'CAD', 'es'))).toContain('12.345,67');
  });

  it('en espanol NO agrupa los millares de cuatro cifras', () => {
    // Y esto no es un fallo del formateador: el espanol tiene «minimo dos cifras para agrupar», asi que
    // 1234 se escribe sin separador y 12345 si lo lleva. Quien lo dé por supuesto escribira una prueba
    // que falle sin que nada este mal — que es exactamente lo que me paso a mi.
    expect(limpiar(formatearImporte(1234.56, 'CAD', 'es'))).toContain('1234,56');
    expect(limpiar(formatearImporte(12345.67, 'CAD', 'es'))).toContain('12.345');
  });

  it('respeta los dos decimales de una moneda', () => {
    expect(limpiar(formatearImporte(10, 'CAD', 'en'))).toContain('10.00');
  });
});

describe('formatearCantidad', () => {
  it('quita los ceros que no informan de nada', () => {
    // Una posicion puede valer 23,00000000 unidades: escribir los ocho ceros no dice nada.
    expect(limpiar(formatearCantidad(23, 'en'))).toBe('23');
  });

  it('conserva los decimales de un fondo, que si los usa', () => {
    expect(limpiar(formatearCantidad(12.3456, 'en'))).toBe('12.3456');
  });
});

describe('formatearPorcentaje', () => {
  it('escribe la proporcion como porcentaje', () => {
    // La API manda 0.3448 y se enseña 34,49 %. Lo multiplica el formateador; hacerlo a mano seria
    // calcular, y ademas con riesgo de equivocarse en el redondeo.
    const texto = limpiar(formatearPorcentaje(0.344861, 'en'));

    expect(texto).toContain('34.49');
    expect(texto).toContain('%');
  });

  it('una perdida sale en negativo', () => {
    expect(limpiar(formatearPorcentaje(-0.1028, 'en'))).toContain('-10.28');
  });
});

describe('formatearPeso', () => {
  it('con un decimal basta', () => {
    // El peso es informativo: 92,0 % dice lo mismo que 92,00 % y ocupa menos.
    const texto = limpiar(formatearPeso(0.9198, 'en'));

    expect(texto).toContain('92.0');
    expect(texto).not.toContain('92.00');
  });
});

describe('formatearFecha', () => {
  it('NO corre el dia', () => {
    // Esta es la trampa: `new Date('2026-08-31')` se interpreta como medianoche UTC, y en America eso es
    // el dia 30. Una fecha de un estado de cuenta no puede aparecer corrida un dia.
    expect(formatearFecha('2026-08-31', 'en')).toContain('31');
    expect(formatearFecha('2026-01-01', 'en')).toContain('1');
  });

  it('escribe el mes segun el idioma', () => {
    expect(formatearFecha('2026-08-31', 'es')).toMatch(/ago/i);
    expect(formatearFecha('2026-08-31', 'en')).toMatch(/aug/i);
  });

  it('si la fecha no tiene la forma esperada, la deja como estaba', () => {
    // Antes devolver algo raro que inventarse una fecha.
    expect(formatearFecha('no-es-una-fecha', 'es')).toBe('no-es-una-fecha');
  });
});
