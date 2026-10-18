// Repara un fichero UTF-8 que se reescribió con PowerShell 5.1: al leerlo sin indicar la codificación, lo
// interpretó como ANSI y volvió a guardar esos caracteres ya rotos.
//
// Uso:  node reparar-codificacion.mjs <fichero> [...más ficheros]
//
// Invierte exactamente el error: cada carácter se vuelve a su byte de cp1252 y los bytes se leen como UTF-8. Si
// algo no encaja se para sin escribir, en vez de dejar el fichero a medias.

import fs from 'node:fs';

// cp1252 define estos caracteres en 0x80-0x9F; el resto de ese rango no existe.
const especiales = {
  0x20ac: 0x80, 0x201a: 0x82, 0x0192: 0x83, 0x201e: 0x84, 0x2026: 0x85, 0x2020: 0x86, 0x2021: 0x87,
  0x02c6: 0x88, 0x2030: 0x89, 0x0160: 0x8a, 0x2039: 0x8b, 0x0152: 0x8c, 0x017d: 0x8e,
  0x2018: 0x91, 0x2019: 0x92, 0x201c: 0x93, 0x201d: 0x94, 0x2022: 0x95, 0x2013: 0x96, 0x2014: 0x97,
  0x02dc: 0x98, 0x2122: 0x99, 0x0161: 0x9a, 0x203a: 0x9b, 0x0153: 0x9c, 0x017e: 0x9e, 0x0178: 0x9f,
};

let fallos = 0;

for (const ruta of process.argv.slice(2)) {
  const roto = fs.readFileSync(ruta, 'utf8').replace(/^\uFEFF/, '');

  // Si ya está bien, no se toca: reparar dos veces estropea.
  if (!/[ÃÂâ€]/.test(roto)) {
    console.log(`${ruta}: no parece roto, se deja igual`);
    continue;
  }

  const bytes = [];
  let fuera = false;

  for (const c of roto) {
    const cp = c.codePointAt(0);

    if (cp < 0x100) bytes.push(cp);
    else if (especiales[cp] !== undefined) bytes.push(especiales[cp]);
    else { fuera = true; break; }
  }

  const arreglado = fuera ? null : Buffer.from(bytes).toString('utf8');

  if (arreglado === null || arreglado.includes('\uFFFD')) {
    console.log(`${ruta}: NO se puede reparar sin adivinar; no se toca`);
    fallos += 1;
    continue;
  }

  fs.writeFileSync(ruta, arreglado, 'utf8');
  console.log(`${ruta}: reparado`);
}

process.exit(fallos === 0 ? 0 : 1);
