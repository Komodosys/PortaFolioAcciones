// Comprueba que los tres idiomas tienen EXACTAMENTE las mismas claves.
//
// Existe porque un idioma al que le falta una clave no falla: Transloco devuelve la propia clave, y la
// pantalla enseña `posiciones.precioManual` a la persona. No hay error, no hay aviso, y en español —el
// idioma en el que se escribe el codigo— no se nota nunca.
//
// Tambien caza las claves REPETIDAS dentro de un mismo fichero: en JSON gana la ultima, asi que una
// etiqueta se perderia sin que nada lo diga. Paso de verdad, con `instrumento.frecuencia`.

import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const CARPETA = 'web/public/i18n';
const IDIOMAS = ['es', 'fr', 'en'];
let fallos = 0;

/** Saca todas las rutas de clave, en notacion de puntos. */
function claves(objeto, prefijo = '') {
  const salida = [];

  for (const [nombre, valor] of Object.entries(objeto)) {
    const ruta = prefijo ? `${prefijo}.${nombre}` : nombre;

    if (valor !== null && typeof valor === 'object') {
      salida.push(...claves(valor, ruta));
    } else {
      salida.push(ruta);
    }
  }

  return salida;
}

/** Busca claves repetidas dentro del mismo objeto, a cualquier profundidad. */
function repetidas(texto) {
  const encontradas = [];
  const pila = [];

  // Se recorre el texto crudo porque `JSON.parse` ya se ha quedado con la ultima: el duplicado solo se ve
  // en el original.
  for (const linea of texto.split('\n')) {
    const abre = /^\s*"([^"]+)"\s*:\s*\{\s*$/.exec(linea);

    if (abre) {
      pila.push({ nombre: abre[1], vistas: new Set() });
      continue;
    }

    if (/^\s*\}/.test(linea)) {
      pila.pop();
      continue;
    }

    const simple = /^\s*"([^"]+)"\s*:/.exec(linea);

    if (simple && pila.length > 0) {
      const marco = pila[pila.length - 1];

      if (marco.vistas.has(simple[1])) {
        encontradas.push(`${pila.map((m) => m.nombre).join('.')}.${simple[1]}`);
      }

      marco.vistas.add(simple[1]);
    }
  }

  return encontradas;
}

const datos = {};

for (const idioma of IDIOMAS) {
  const ruta = join(CARPETA, `${idioma}.json`);
  const texto = readFileSync(ruta, 'utf8');

  try {
    datos[idioma] = new Set(claves(JSON.parse(texto)));
  } catch (error) {
    console.log(`  [MAL] ${idioma}.json no es JSON valido: ${error.message}`);
    fallos++;
    continue;
  }

  const dup = repetidas(texto);

  if (dup.length > 0) {
    console.log(`  [MAL] ${idioma}.json tiene claves repetidas: ${dup.join(', ')}`);
    fallos++;
  }
}

// Se compara cada idioma con el español, que es el idioma en el que se escribe el codigo.
const referencia = datos.es;

if (referencia) {
  for (const idioma of IDIOMAS.filter((i) => i !== 'es')) {
    const conjunto = datos[idioma];

    if (!conjunto) {
      continue;
    }

    const faltan = [...referencia].filter((clave) => !conjunto.has(clave));
    const sobran = [...conjunto].filter((clave) => !referencia.has(clave));

    if (faltan.length > 0) {
      console.log(`  [MAL] a ${idioma}.json le faltan ${faltan.length}: ${faltan.slice(0, 8).join(', ')}${faltan.length > 8 ? '…' : ''}`);
      fallos++;
    }

    if (sobran.length > 0) {
      console.log(`  [MAL] a ${idioma}.json le sobran ${sobran.length} (no estan en español): ${sobran.slice(0, 8).join(', ')}${sobran.length > 8 ? '…' : ''}`);
      fallos++;
    }
  }
}

const total = referencia ? referencia.size : 0;

console.log(`  Claves en español: ${total}. Ficheros: ${readdirSync(CARPETA).filter((f) => f.endsWith('.json')).length}.`);
console.log(fallos === 0 ? '  Los tres idiomas tienen las mismas claves.' : `  ${fallos} problema(s).`);
process.exit(fallos === 0 ? 0 : 1);
