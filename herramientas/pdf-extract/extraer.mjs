// Extrae texto de los PDF de estados de cuenta, reconstruyendo lineas por coordenadas.
//
// Uso:  node extraer.mjs <carpeta-entrada> <carpeta-salida>
//
// Para cada PDF genera un .txt con el texto de cada pagina. Dentro de una linea, un tabulador
// separa columnas cuando el hueco horizontal entre dos fragmentos es grande, que es lo que
// permite reconstruir las tablas de posiciones.

import fs from 'node:fs';
import path from 'node:path';
import { getDocument } from './node_modules/pdfjs-dist/legacy/build/pdf.mjs';

const entrada = process.argv[2];
const salida = process.argv[3];

if (!entrada || !salida) {
  console.error('Uso: node extraer.mjs <carpeta-entrada> <carpeta-salida>');
  process.exit(1);
}

fs.mkdirSync(salida, { recursive: true });

// Recorre subcarpetas, para poder procesar varias cuentas de una vez, y conserva la estructura en la
// salida: las cuentas repiten nombre de fichero (todas tienen "Individual RRSP_2026-01-01.pdf").
function buscarPdf(dir, prefijo = '') {
  const encontrados = [];
  const entradas = fs.readdirSync(dir, { withFileTypes: true })
    .sort((a, b) => a.name.localeCompare(b.name));
  for (const e of entradas) {
    const rel = prefijo ? path.join(prefijo, e.name) : e.name;
    const completo = path.join(dir, e.name);
    if (e.isDirectory()) encontrados.push(...buscarPdf(completo, rel));
    else if (e.name.toLowerCase().endsWith('.pdf')) encontrados.push({ completo, rel });
  }
  return encontrados;
}

const archivos = buscarPdf(entrada);

console.log(`PDF encontrados: ${archivos.length}`);

for (const { completo: ruta, rel } of archivos) {
  const datos = new Uint8Array(fs.readFileSync(ruta));

  const doc = await getDocument({
    data: datos,
    useSystemFonts: false,
    disableFontFace: true,
    isEvalSupported: false,
    useWorkerFetch: false,
    verbosity: 0,
  }).promise;

  const lineas = [];

  for (let p = 1; p <= doc.numPages; p++) {
    const pagina = await doc.getPage(p);
    const contenido = await pagina.getTextContent();

    // Agrupar fragmentos por su coordenada vertical (redondeada) para reconstruir filas.
    const filas = new Map();
    for (const item of contenido.items) {
      if (!item.str) continue;
      const y = Math.round(item.transform[5] / 2) * 2;
      const x = item.transform[4];
      if (!filas.has(y)) filas.set(y, []);
      filas.get(y).push({ x, ancho: item.width || 0, str: item.str });
    }

    const alturas = [...filas.keys()].sort((a, b) => b - a);
    lineas.push(`===== PAGINA ${p} =====`);

    for (const y of alturas) {
      const items = filas.get(y).sort((a, b) => a.x - b.x);
      let texto = '';
      let finAnterior = null;
      for (const it of items) {
        if (finAnterior !== null) {
          const hueco = it.x - finAnterior;
          if (hueco > 8) texto += '\t';
          else if (hueco > 1) texto += ' ';
        }
        texto += it.str;
        finAnterior = it.x + it.ancho;
      }
      const limpia = texto.replace(/[ \t]+$/, '');
      if (limpia.trim()) lineas.push(limpia);
    }
  }

  try {
    if (typeof doc.destroy === 'function') await doc.destroy();
    else if (typeof doc.cleanup === 'function') await doc.cleanup();
  } catch {
    // Irrelevante: el proceso termina enseguida.
  }

  const destino = path.join(salida, rel.replace(/\.pdf$/i, '.txt'));
  fs.mkdirSync(path.dirname(destino), { recursive: true });
  fs.writeFileSync(destino, lineas.join('\n'), 'utf8');
  console.log(`OK  ${rel}  (${doc.numPages} pag, ${lineas.length} lineas)`);
}

console.log('Terminado.');
