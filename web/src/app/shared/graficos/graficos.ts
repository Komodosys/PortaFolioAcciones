/**
 * La geometría de los dos gráficos: un donut y una línea.
 *
 * **Está aquí y no en la plantilla porque es lo único de un gráfico que se puede equivocar de verdad.** Un arco
 * mal calculado no falla: dibuja una porción del tamaño que no es, y nadie lo nota mirando. Poniéndolo en
 * funciones puras, la prueba fija los números.
 *
 * Y **no calcula nada financiero**: recibe importes ya sumados y convertidos por el servidor y solo los reparte
 * en ángulos y en píxeles. La regla del proyecto es que el cliente formatea y presenta, nunca suma dinero.
 */

/** Una porción del donut, ya con su etiqueta y su parte del total. */
export interface Porcion {
  readonly etiqueta: string;
  readonly valor: number;

  /** Qué parte del total es, de 0 a 1. */
  readonly parte: number;
}

/**
 * Agrupa los importes en porciones, dejando las menores en una sola de «otros».
 *
 * **El límite no es un capricho estético.** Con nueve porciones todavía se distinguen; con treinta, los sectores
 * son franjas de dos grados y la leyenda ocupa más que el gráfico. Una porción de «otros» dice la verdad —hay
 * cosas pequeñas— sin fingir que se pueden comparar.
 *
 * @param items Lo que se va a repartir.
 * @param valor De cada item, su importe. Los negativos y los ceros se descartan: un patrimonio negativo no es
 * una porción, y una de cero no se ve.
 * @param etiqueta Cómo se llama cada item en el gráfico.
 * @param maximo Cuántas porciones como mucho, contando la de «otros».
 * @param otros Texto de la porción que recoge el resto.
 */
export function repartir<T>(
  items: readonly T[],
  valor: (item: T) => number,
  etiqueta: (item: T) => string,
  maximo: number,
  otros: string,
): readonly Porcion[] {
  const conValor = items
    .map((item) => ({ etiqueta: etiqueta(item), valor: valor(item) }))
    .filter((item) => item.valor > 0)
    .sort((a, b) => b.valor - a.valor);

  const total = conValor.reduce((suma, item) => suma + item.valor, 0);

  if (total <= 0) {
    return [];
  }

  const cabe = Math.max(2, maximo);
  const principales = conValor.slice(0, cabe - 1);
  const resto = conValor.slice(cabe - 1);

  const porciones: Porcion[] = principales.map((item) => ({
    etiqueta: item.etiqueta,
    valor: item.valor,
    parte: item.valor / total,
  }));

  // La de «otros» solo si hay algo que recoger: una porción de cero sería una entrada en la leyenda que no
  // representa nada.
  if (resto.length > 0) {
    const suma = resto.reduce((total_, item) => total_ + item.valor, 0);
    porciones.push({ etiqueta: otros, valor: suma, parte: suma / total });
  }

  return porciones;
}

/** Un punto del donut en coordenadas SVG. */
function punto(centro: number, radio: number, angulo: number): { x: number; y: number } {
  // El ángulo se mide desde las 12 en punto y en sentido horario, que es como se lee un gráfico de tarta.
  return {
    x: centro + radio * Math.sin(angulo),
    y: centro - radio * Math.cos(angulo),
  };
}

/**
 * El `d` de un arco de donut, en un lienzo de 100×100.
 *
 * @param desde Ángulo de comienzo, en radianes, desde las 12 en punto.
 * @param hasta Ángulo de fin.
 * @param exterior Radio de fuera.
 * @param interior Radio de dentro. Deja el agujero del centro.
 */
export function arco(desde: number, hasta: number, exterior = 46, interior = 32): string {
  const giro = hasta - desde;
  const centro = 50;

  // Una vuelta entera no se puede dibujar con un solo arco: el punto de salida y el de llegada son el mismo y
  // el navegador no dibuja nada. Se hace con dos mitades.
  if (giro >= Math.PI * 2 - 0.0001) {
    const mitad = desde + Math.PI;
    return `${arco(desde, mitad, exterior, interior)} ${arco(mitad, desde + Math.PI * 2 - 0.0001, exterior, interior)}`;
  }

  const grande = giro > Math.PI ? 1 : 0;
  const fueraInicio = punto(centro, exterior, desde);
  const fueraFin = punto(centro, exterior, hasta);
  const dentroFin = punto(centro, interior, hasta);
  const dentroInicio = punto(centro, interior, desde);

  return [
    `M ${fueraInicio.x.toFixed(3)} ${fueraInicio.y.toFixed(3)}`,
    `A ${exterior} ${exterior} 0 ${grande} 1 ${fueraFin.x.toFixed(3)} ${fueraFin.y.toFixed(3)}`,
    `L ${dentroFin.x.toFixed(3)} ${dentroFin.y.toFixed(3)}`,
    `A ${interior} ${interior} 0 ${grande} 0 ${dentroInicio.x.toFixed(3)} ${dentroInicio.y.toFixed(3)}`,
    'Z',
  ].join(' ');
}

/** Los arcos de una lista de porciones, en orden. */
export function arcosDe(porciones: readonly Porcion[], exterior = 46, interior = 32): readonly string[] {
  const arcos: string[] = [];
  let angulo = 0;

  for (const porcion of porciones) {
    const siguiente = angulo + porcion.parte * Math.PI * 2;
    arcos.push(arco(angulo, siguiente, exterior, interior));
    angulo = siguiente;
  }

  return arcos;
}

/** Un punto de la línea, en un lienzo de ancho×alto. */
export interface PuntoDeLinea {
  readonly x: number;
  readonly y: number;
}

/** Una serie dibujada: sus puntos y el trazado que los une. */
export interface SerieDibujada {
  readonly puntos: readonly PuntoDeLinea[];
  readonly trazo: string;
}

/**
 * Lleva una serie de importes a coordenadas del lienzo.
 *
 * @param valores Los importes, del más antiguo al más reciente.
 * @param minimo El menor de **todas** las series que se van a dibujar juntas.
 * @param maximo El mayor de todas.
 * @param ancho Ancho del lienzo.
 * @param alto Alto del lienzo.
 * @param margen Hueco por arriba y por abajo, para que el trazo no se pegue al borde.
 *
 * Se pasan el mínimo y el máximo de todas las series y no los de cada una: **dos líneas con escalas distintas
 * se cruzarían donde no se cruzan**, y el gráfico diría justo lo contrario de lo que pasó.
 */
export function dibujarSerie(
  valores: readonly number[],
  minimo: number,
  maximo: number,
  ancho: number,
  alto: number,
  margen = 6,
): SerieDibujada {
  if (valores.length === 0) {
    return { puntos: [], trazo: '' };
  }

  const rango = maximo - minimo;
  const util = Math.max(1, alto - margen * 2);

  // Una serie plana —todo el mismo importe— no tiene rango. Se dibuja en el centro, que es la verdad: no subió
  // ni bajó. Dividir por cero daría NaN y el navegador no dibujaría nada, sin decir por qué.
  const y = (valor: number): number =>
    rango === 0 ? alto / 2 : margen + util - ((valor - minimo) / rango) * util;

  // Con un solo punto no hay línea que unir: se devuelve el punto y un trazado vacío, para que la pantalla
  // pueda dibujar un circulo suelto en vez de una raya que no significa nada.
  const x = (indice: number): number =>
    valores.length === 1 ? ancho / 2 : (indice / (valores.length - 1)) * ancho;

  const puntos = valores.map((valor, indice) => ({ x: x(indice), y: y(valor) }));

  const trazo = puntos
    .map((punto_, indice) => `${indice === 0 ? 'M' : 'L'} ${punto_.x.toFixed(2)} ${punto_.y.toFixed(2)}`)
    .join(' ');

  return { puntos, trazo };
}
