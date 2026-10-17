import { PosicionEnVista } from './modelos';
import { compararPosiciones, ordenarPor, sentidoInicial } from './orden';

/** Una posición de mentira, con lo mínimo para poder ordenarla. */
function posicion(parcial: Partial<PosicionEnVista> & { id: string }): PosicionEnVista {
  return {
    symbol: 'AAA',
    instrumentName: 'Instrumento',
    quantity: 1,
    priceAmount: 1,
    priceCurrency: 'CAD',
    priceSource: 'manual',
    priceDate: null,
    marketState: 'Closed',
    isStale: false,
    previousClose: null,
    dailyChange: null,
    baseDailyChange: null,
    dailyReturn: null,
    marketValue: 1,
    cost: 1,
    baseMarketValue: 1,
    baseCost: 1,
    baseGain: 0,
    baseReturn: 0,
    weight: null,
    localGain: null,
    localReturn: null,
    ...parcial,
  };
}

describe('compararPosiciones', () => {
  it('ordena por el NUMERO, no por el texto formateado', () => {
    // Esta es la trampa que justifica este fichero. Los dos valores, formateados en español, son
    // «1.234,56» y «9,00». Comparados como TEXTO, «9» va despues de «1», asi que 9 saldria como mayor.
    const grande = posicion({ id: 'a', marketValue: 1234.56 });
    const pequeno = posicion({ id: 'b', marketValue: 9 });

    expect(compararPosiciones(pequeno, grande, 'valor', 'es')).toBeLessThan(0);
    expect(compararPosiciones(grande, pequeno, 'valor', 'es')).toBeGreaterThan(0);
  });

  it('ordena los negativos por debajo de cero', () => {
    const perdida = posicion({ id: 'a', baseGain: -1309 });
    const ganancia = posicion({ id: 'b', baseGain: 4446.81 });

    expect(compararPosiciones(perdida, ganancia, 'ganancia', 'es')).toBeLessThan(0);
  });

  it('ordena los simbolos segun el idioma', () => {
    const conEnie = posicion({ id: 'a', symbol: 'ÑU' });
    const conEne = posicion({ id: 'b', symbol: 'NUE' });

    // Con las reglas del español, la Ñ va DESPUES de la N. Sin pasar el idioma, el orden por defecto la
    // manda al final del alfabeto, detras de la Z.
    expect(compararPosiciones(conEne, conEnie, 'simbolo', 'es')).toBeLessThan(0);
  });

  it('una posicion sin peso no se va al fondo por ser nula', () => {
    const sinPeso = posicion({ id: 'a', weight: null });
    const conPeso = posicion({ id: 'b', weight: 0.5 });

    expect(compararPosiciones(sinPeso, conPeso, 'peso', 'es')).toBeLessThan(0);
  });

  it('ordena por lo que ha cambiado HOY, en moneda base', () => {
    const sube = posicion({ id: 'a', baseDailyChange: 139.08 });
    const baja = posicion({ id: 'b', baseDailyChange: -50 });

    // Lo que se compara es el importe convertido, no el de la moneda del activo: dos posiciones en
    // monedas distintas solo se pueden comparar asi.
    expect(compararPosiciones(baja, sube, 'hoy', 'es')).toBeLessThan(0);
  });

  it('una posicion sin cambio del dia cuenta como cero, y no se va al fondo', () => {
    // No se sabe cuanto ha cambiado. Mandarla al fondo por eso la sacaria de su sitio sin motivo.
    const sinCambio = posicion({ id: 'a', baseDailyChange: null });
    const perdida = posicion({ id: 'b', baseDailyChange: -10 });

    expect(compararPosiciones(perdida, sinCambio, 'hoy', 'es')).toBeLessThan(0);
  });

  it('dos posiciones iguales dan cero', () => {
    const una = posicion({ id: 'a', marketValue: 100 });
    const otra = posicion({ id: 'b', marketValue: 100 });

    expect(compararPosiciones(una, otra, 'valor', 'es')).toBe(0);
  });
});

describe('ordenarPor', () => {
  it('al pulsar una columna nueva, los numeros empiezan de mayor a menor', () => {
    expect(sentidoInicial('valor')).toBe(true);
    expect(ordenarPor({ campo: 'valor', descendente: true }, 'ganancia')).toEqual({
      campo: 'ganancia',
      descendente: true,
    });
  });

  it('y los textos, de la A a la Z', () => {
    // Empezar los nombres de la Z a la A seria no entender para que se pulsa esa columna.
    expect(sentidoInicial('nombre')).toBe(false);
    expect(ordenarPor({ campo: 'valor', descendente: true }, 'nombre')).toEqual({
      campo: 'nombre',
      descendente: false,
    });
  });

  it('al volver a pulsar la misma, cambia el sentido', () => {
    expect(ordenarPor({ campo: 'valor', descendente: true }, 'valor')).toEqual({
      campo: 'valor',
      descendente: false,
    });

    expect(ordenarPor({ campo: 'valor', descendente: false }, 'valor')).toEqual({
      campo: 'valor',
      descendente: true,
    });
  });
});
