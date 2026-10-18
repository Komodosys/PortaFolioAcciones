import { arco, arcosDe, dibujarSerie, repartir } from './graficos';

const item = (etiqueta: string, valor: number) => ({ etiqueta, valor });
const valor = (i: { valor: number }) => i.valor;
const etiqueta = (i: { etiqueta: string }) => i.etiqueta;

describe('repartir', () => {
  it('las partes suman el total', () => {
    const porciones = repartir(
      [item('A', 50), item('B', 30), item('C', 20)],
      valor,
      etiqueta,
      8,
      'otros',
    );

    expect(porciones.length).toBe(3);
    expect(porciones.reduce((suma, p) => suma + p.parte, 0)).toBeCloseTo(1, 10);
    expect(porciones.map((p) => p.etiqueta)).toEqual(['A', 'B', 'C']);
  });

  it('las menores se recogen en «otros» y no pierden importe', () => {
    // Es la razon de existir del limite: con treinta porciones, los sectores son franjas de dos grados y la
    // leyenda ocupa mas que el grafico.
    const items = Array.from({ length: 10 }, (_, i) => item(`P${i}`, 100 - i));

    const porciones = repartir(items, valor, etiqueta, 4, 'otros');

    expect(porciones.length).toBe(4);
    expect(porciones[3].etiqueta).toBe('otros');

    // Nada se pierde por el camino: la suma de las porciones es la suma de todo lo que se paso.
    const totalPorciones = porciones.reduce((suma, p) => suma + p.valor, 0);
    const totalItems = items.reduce((suma, i) => suma + i.valor, 0);
    expect(totalPorciones).toBe(totalItems);
  });

  it('no hay porcion de «otros» si no queda nada que recoger', () => {
    const porciones = repartir([item('A', 10), item('B', 5)], valor, etiqueta, 8, 'otros');

    expect(porciones.map((p) => p.etiqueta)).toEqual(['A', 'B']);
  });

  it('se descartan los ceros y los negativos', () => {
    // Un patrimonio negativo no es una porcion, y una de cero no se ve: dejarlas seria una entrada en la
    // leyenda que no representa nada.
    const porciones = repartir(
      [item('A', 10), item('Cero', 0), item('Negativo', -5)],
      valor,
      etiqueta,
      8,
      'otros',
    );

    expect(porciones.length).toBe(1);
    expect(porciones[0].etiqueta).toBe('A');
    expect(porciones[0].parte).toBe(1);
  });

  it('sin nada que repartir devuelve una lista vacía, no una porción de cero', () => {
    expect(repartir([item('A', 0)], valor, etiqueta, 8, 'otros')).toEqual([]);
  });
});

describe('arco', () => {
  it('un cuarto de vuelta empieza y termina donde toca', () => {
    // El arco va de las 12 en punto hacia la derecha: a 90 grados el punto de fuera está a la derecha del centro.
    const d = arco(0, Math.PI / 2);

    expect(d).toContain('M 50.000');
    expect(d).toContain('96.000');
  });

  it('una vuelta entera no se dibuja como un solo arco', () => {
    // El punto de salida y el de llegada serian el mismo y el navegador no pintaria nada. Se parte en dos.
    const d = arco(0, Math.PI * 2);

    expect(d.split('M').length - 1).toBe(2);
  });

  it('los arcos de una lista cubren la vuelta completa', () => {
    const porciones = repartir(
      [item('A', 1), item('B', 1), item('C', 1), item('D', 1)],
      valor,
      etiqueta,
      8,
      'otros',
    );

    const arcos = arcosDe(porciones);

    expect(arcos.length).toBe(4);
    for (const d of arcos) {
      expect(d.startsWith('M')).toBe(true);
      expect(d.endsWith('Z')).toBe(true);
    }
  });
});

describe('dibujarSerie', () => {
  it('el mayor va arriba y el menor abajo', () => {
    // El lienzo de SVG crece hacia abajo: el importe mayor tiene que tener la y MENOR.
    const serie = dibujarSerie([0, 100], 0, 100, 100, 100, 0);

    expect(serie.puntos[0].y).toBe(100);
    expect(serie.puntos[1].y).toBe(0);
  });

  it('reparte los puntos a lo ancho', () => {
    const serie = dibujarSerie([1, 2, 3], 1, 3, 100, 100, 0);

    expect(serie.puntos.map((p) => p.x)).toEqual([0, 50, 100]);
  });

  it('una serie plana se dibuja en el centro y no da NaN', () => {
    // Es el caso que rompe la cuenta: sin rango, dividir por cero da NaN y el navegador no dibuja nada, sin
    // decir por que. Y una linea plana significa «no cambio», que es la verdad.
    const serie = dibujarSerie([50, 50, 50], 50, 50, 100, 100, 0);

    expect(serie.puntos.every((p) => p.y === 50)).toBe(true);
    expect(serie.trazo).not.toContain('NaN');
  });

  it('con un solo punto no hay trazo que unir', () => {
    const serie = dibujarSerie([10], 10, 10, 100, 100, 0);

    expect(serie.puntos.length).toBe(1);
    expect(serie.puntos[0].x).toBe(50);
  });

  it('dos series con la misma escala se comparan de verdad', () => {
    // Se pasan el minimo y el maximo de TODAS: con escalas distintas, dos lineas se cruzarian donde no se
    // cruzaron, y el grafico diria lo contrario de lo que paso.
    const valorSerie = dibujarSerie([100, 200], 100, 200, 100, 100, 0);
    const aportado = dibujarSerie([100, 100], 100, 200, 100, 100, 0);

    // Donde el valor es 100 y lo aportado es 100, las dos lineas coinciden exactamente.
    expect(valorSerie.puntos[0].y).toBe(aportado.puntos[0].y);
    expect(valorSerie.puntos[1].y).toBeLessThan(aportado.puntos[1].y);
  });
});
