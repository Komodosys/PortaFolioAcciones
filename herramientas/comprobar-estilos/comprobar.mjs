// Comprueba una trampa de Tailwind que no da error y se ve tarde.
//
// UN COLOR DE TEXTO BASE Y OTRO CONDICIONAL EN EL MISMO ELEMENTO
//
// Tailwind ordena sus utilidades por NOMBRE de clase, no por el orden en que se escriben. Si una etiqueta
// lleva `text-slate-700` de base y `[class.text-emerald-600]` condicional, gana la que quede después en el
// CSS generado — y resulta que «emerald» va ANTES que «slate». El color condicional no se aplica nunca y
// el elemento se queda con el de base.
//
// Se compila, las pruebas pasan, y simplemente el color no es el que se espera. Pasó tres veces en este
// proyecto antes de que hubiera nada que lo mirara.
//
// La solución es no ponerlos en el mismo elemento: el color base va en el padre y se hereda.
//
// NO se avisa de una clase base con su variante responsiva (`hidden` + `md:flex`): esas sí conviven,
// porque la variante va en una media query y se genera después.

import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

let fallos = 0;

const COLORES = 'slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose';

/** Un matiz de color de texto: `text-slate-700`. No confunde `text-sm` ni `text-right`, que no llevan número. */
const COLOR_DE_TEXTO = new RegExp(`\\btext-(${COLORES})-\\d{3}\\b`);

/** Una atadura condicional de color de texto: `[class.text-emerald-600]`. */
const COLOR_CONDICIONAL = new RegExp(`\\[class\\.text-(${COLORES})-\\d{3}\\]`, 'g');

for (const fichero of readdirSync('web/src', { recursive: true })) {
  if (!fichero.endsWith('.html')) {
    continue;
  }

  const ruta = join('web/src', fichero);
  const texto = readFileSync(ruta, 'utf8');

  // Se recorren las etiquetas de una en una. Cada una puede ocupar varias líneas, así que se busca el
  // principio y se cuenta la línea para poder ir a mirarlo.
  for (const etiqueta of texto.matchAll(/<[a-z][^>]*>/gi)) {
    if (!etiqueta[0].match(COLOR_CONDICIONAL)) {
      continue;
    }

    // **Se quitan TODAS las condicionales antes de buscar un color base**, y con la expresión global: si
    // solo se quitara la primera, la segunda —`[class.text-red-600]`— se leería como si fuera un color
    // base y saldrían avisos falsos en todos los elementos con dos condicionales.
    if (!COLOR_DE_TEXTO.test(etiqueta[0].replace(COLOR_CONDICIONAL, ''))) {
      continue;
    }

    const linea = texto.slice(0, etiqueta.index).split('\n').length;
    const resumen = etiqueta[0].replace(/\s+/g, ' ').slice(0, 120);

    console.log(`  [MAL] ${ruta}:${linea} mezcla color base y condicional en el mismo elemento`);
    console.log(`        ${resumen}`);
    fallos++;
  }
}

console.log(
  fallos === 0
    ? '  Ningun elemento mezcla color base con color condicional.'
    : `  ${fallos} elemento(s). El condicional NO se aplicara: mueve el color base al padre.`);

process.exit(fallos === 0 ? 0 : 1);
