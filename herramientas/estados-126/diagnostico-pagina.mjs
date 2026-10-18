// Diagnostico de una pagina concreta: tamano, rotacion y coordenadas de sus fragmentos.
import fs from 'node:fs';
import { getDocument } from '../pdf-extract/node_modules/pdfjs-dist/legacy/build/pdf.mjs';

const doc = await getDocument({
  data: new Uint8Array(fs.readFileSync(process.argv[2])),
  useSystemFonts: false, disableFontFace: true, isEvalSupported: false,
  useWorkerFetch: false, verbosity: 0,
}).promise;

for (const p of process.argv.slice(3).map(Number)) {
  const page = await doc.getPage(p);
  const vp = page.getViewport({ scale: 1 });
  const tc = await page.getTextContent();
  console.log(`--- pagina ${p}  viewport ${vp.width}x${vp.height}  rotacion ${page.rotate}`);
  for (const it of tc.items) {
    if (!it.str.trim()) continue;
    console.log(`${it.transform[5].toFixed(1).padStart(7)} ${it.transform[4].toFixed(2).padStart(8)} ${(it.width || 0).toFixed(2).padStart(7)}  ${JSON.stringify(it.str)}`);
  }
}
