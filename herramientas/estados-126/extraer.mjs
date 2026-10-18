// Extrae de los estados de cuenta de la cuenta RRSP ···126 (REER particulier, Questrade) las
// operaciones de la seccion 04 ACTIVITES, usando las COORDENADAS de cada fragmento de texto.
//
// El numero de cuenta no se escribe aqui: el repositorio se publica y las cuentas se enmascaran.
//
// Uso:  node extraer.mjs <carpeta-estados> <fichero-json-salida>
//
// Lo que se guarda de cada movimiento, en el orden en que el estado lo imprime:
//   seccion  04 = el mes, 05 = sin liquidar al cierre (NO se suma: se repite al mes siguiente)
//   orden    posicion de la fila dentro de la pagina, de arriba abajo. Sirve para que un traspaso y su
//            conversion del mismo dia salgan en el orden en que ocurrieron y no en orden alfabetico.
//
// Por que coordenadas y no el texto seguido:
//   - La descripcion de una operacion ocupa VARIAS LINEAS y puede empezar POR ENCIMA de la linea
//     principal (el dividendo de MSFT en diciembre de 2025 y el de MGK en octubre de 2021). Cortar por
//     lineas partiria una operacion en tres y la contaria tres veces o ninguna.
//   - El TIPO tambien se parte en dos lineas: "Opérations Sur" / "Devises" es un solo concepto, y esta
//     escrito asi en el estado.
//   - La cantidad de una compra puede ser entera (49) o decimal (2,0000). Un patron unico de "cantidad
//     con cuatro decimales" falla en el primer caso, que es el de la primera compra de la cuenta.
//
// Lo que se aprovecha del formato, comprobado en un estado de 2020 y otro de 2025, y valido en los 73:
//   - La geometria de la tabla es la misma en los siete años, pero NO se escribe a fuego aqui: los
//     bordes de las nueve columnas de importe se DEDUCEN de la cabecera de cada pagina.
//   - Los importes van alineados a la DERECHA, cada columna a su coordenada exacta, y el borde derecho
//     no cambia de una fila a otra. Eso es lo que permite saber a que columna pertenece cada numero
//     aunque la fila deje columnas vacias.
//   - Las columnas son nueve: Cantidad + (CAD: Precio, Bruto, Comision, Neto) + (USD: los cuatro).
//   - Los importes son franceses: ESPACIO de millares y coma decimal, y los negativos entre parentesis.
//     "312,000" es 312,000 (trescientos doce), no 312000.
//
// Y dos secciones distintas que se confunden a proposito:
//   - 04 ACTIVITES es la tabla de lo que paso en el mes. Es la buena.
//   - 05 OPERATIONS REGLEES APRES LA FIN DU MOIS tiene OTRA geometria y repite lo que el estado del mes
//     siguiente lista como actividad normal. Se extrae aparte, marcada, para poder comprobar que de
//     verdad se repite: si se sumara, cada venta de fin de mes contaria dos veces.

import fs from 'node:fs';
import path from 'node:path';
// Se reutiliza el pdfjs-dist que ya usa la herramienta de extraccion de los otros estados, para no
// tener dos copias de la misma dependencia (y porque la version que trae pdf-parse devuelve los
// acentos doblemente codificados).
import { getDocument } from '../pdf-extract/node_modules/pdfjs-dist/legacy/build/pdf.mjs';

const entrada = process.argv[2];
const salida = process.argv[3];
if (!entrada || !salida) {
  console.error('Uso: node extraer.mjs <carpeta-estados> <fichero-json-salida>');
  process.exit(1);
}

const TOLERANCIA_FILA = 1.5; // dos fragmentos a menos de esto, en vertical, son la misma linea
const TOLERANCIA_COLUMNA = 3; // borde derecho de un importe: mas cerca que esto de su columna

// Un importe, una cantidad o el guion del vacio.
const RE_NUMERO = /^\(?-?\d[\d .,]*\)?$|^-$/;
const esNumero = (s) => RE_NUMERO.test(s.trim());

// Convierte "1 234,56" y "(1 234,56)" a numero. El guion es un HUECO, no un cero: devuelve null.
function aNumero(s) {
  if (s === undefined || s === null) return null;
  const t = String(s).trim();
  if (t === '' || t === '-') return null;
  let negativo = false;
  let cuerpo = t;
  if (/^\(.*\)$/.test(cuerpo)) { negativo = true; cuerpo = cuerpo.slice(1, -1); }
  cuerpo = cuerpo.replace(/[\s\u00a0\u202f]/g, '');
  if (cuerpo.includes(',') && cuerpo.includes('.')) {
    cuerpo = cuerpo.lastIndexOf(',') > cuerpo.lastIndexOf('.')
      ? cuerpo.replace(/\./g, '').replace(',', '.')
      : cuerpo.replace(/,/g, '');
  } else {
    cuerpo = cuerpo.replace(',', '.');
  }
  const n = Number(cuerpo);
  if (!Number.isFinite(n)) return null;
  return negativo ? -n : n;
}

function agruparEnLineas(items) {
  const ordenados = [...items].sort((a, b) => b.y - a.y || a.x - b.x);
  const lineas = [];
  for (const it of ordenados) {
    const ultima = lineas[lineas.length - 1];
    if (ultima && Math.abs(ultima.y - it.y) <= TOLERANCIA_FILA) {
      ultima.items.push(it);
      ultima.y = (ultima.y * (ultima.items.length - 1) + it.y) / ultima.items.length;
    } else {
      lineas.push({ y: it.y, items: [it] });
    }
  }
  for (const l of lineas) l.items.sort((a, b) => a.x - b.x);
  return lineas;
}

// Bordes derechos de las nueve columnas de importe, deducidos de la linea de cabecera que lleva
// "Quant.". Los rótulos van alineados a la derecha igual que los importes, y el asterisco de nota
// ("¹") puede venir como fragmento aparte o pegado al rótulo: se tratan los dos casos.
function columnasDeCabecera(items, rel) {
  const lineas = agruparEnLineas(items);
  const cabs = lineas.filter((l) => l.items.some((i) => i.str.trim() === 'Quant.'));
  if (cabs.length !== 1) return { error: `${rel}: se esperaba una cabecera "Quant." y hay ${cabs.length}` };

  const linea = cabs[0];
  const bordes = [];
  const it = linea.items;
  for (let k = 0; k < it.length; k++) {
    const s = it[k].str.trim();
    if (s === '¹' || s === '') continue;
    let borde = it[k].x + it[k].w;
    const siguiente = it[k + 1];
    // "Prix" y "Quant." no llevan nota; "Brut¹" puede venir pegado, y entonces ya esta incluida.
    if (siguiente && siguiente.str.trim() === '¹' && Math.abs(siguiente.x - borde) < 0.5) {
      borde = siguiente.x + siguiente.w;
    }
    bordes.push(+borde.toFixed(2));
  }
  // Se descartan los bordes de la leyenda de la nota (el "¹" que precede a "Activités"), que queda
  // muy a la izquierda de la tabla.
  const tabla = bordes.filter((b) => b > 330);
  if (tabla.length !== 9) {
    return { error: `${rel}: la cabecera dio ${tabla.length} columnas de importe en vez de 9 (${tabla.join(', ')})` };
  }
  return { columnas: tabla };
}

function operacionesDePagina(items, rel, seccion) {
  const lineas = agruparEnLineas(items);

  const lineaCabecera = lineas.find((l) => l.items.some((i) => i.str.trim() === 'Symbole'));
  if (!lineaCabecera) return { operaciones: [], errores: [`${rel}: pagina de operaciones sin cabecera`] };

  const cols = columnasDeCabecera(items, rel);
  if (cols.error) return { operaciones: [], errores: [cols.error] };
  const bordes = cols.columnas;
  const NOMBRES = ['cantidad', 'cadPrecio', 'cadBruto', 'cadComision', 'cadNeto',
    'usdPrecio', 'usdBruto', 'usdComision', 'usdNeto'];

  const errores = [];
  // Todo lo que este por debajo de la cabecera. El pie de pagina se corta mas abajo.
  let cuerpo = lineas.filter((l) => l.y < lineaCabecera.y - 2);

  const leerLinea = (l) => {
    const c = { fechaOperacion: '', fechaLiquidacion: '', tipo: [], simbolo: [], descripcion: [] };
    for (const n of NOMBRES) c[n] = null;
    for (const it of l.items) {
      const s = it.str.trim();
      if (esNumero(s)) {
        const borde = +(it.x + it.w).toFixed(2);
        let mejor = -1;
        let distancia = Infinity;
        bordes.forEach((b, k) => {
          const d = Math.abs(b - borde);
          if (d < distancia) { distancia = d; mejor = k; }
        });
        if (distancia <= TOLERANCIA_COLUMNA) {
          c[NOMBRES[mejor]] = aNumero(s);
          continue;
        }
        // Un numero que no cae en ninguna columna solo puede ser texto de la descripcion ("20" de
        // "20 SHS"). Se queda en la descripcion, pero se apunta para poder revisarlo.
      }
      if (it.x < 80) c.fechaOperacion += s;
      else if (it.x < 133) c.fechaLiquidacion += s;
      else if (it.x < 183) c.tipo.push(s);
      else if (it.x < 232) c.simbolo.push(s);
      else c.descripcion.push(s);
    }
    return c;
  };

  const RE_FECHA = /^(\d{2})-(\d{2})-(\d{4})$/;
  const fechasDe = (s) => {
    const f = s.match(RE_FECHA);
    return f ? `${f[3]}-${f[1]}-${f[2]}` : null;
  };

  const filas = cuerpo.map(leerLinea);

  // Anclas: la linea que lleva la fecha de la operacion, y las dos de saldo. Por debajo de la ultima
  // (el saldo de cierre) ya solo esta el pie de pagina, con el numero de cuenta, que es un numero y
  // se colaria como un movimiento.
  const anclas = [];
  filas.forEach((f, k) => {
    const esSaldo = /^Solde d/.test(f.descripcion[0] || '') || /^Solde de fermeture/.test(f.descripcion[0] || '');
    if (RE_FECHA.test(f.fechaOperacion) || esSaldo) anclas.push({ indice: k, y: cuerpo[k].y, esSaldo });
  });
  if (anclas.length === 0) return { operaciones: [], errores: [`${rel}: tabla sin filas`] };

  // El cierre manda: nada despues de el pertenece a la tabla, y lo que hay despues es el pie de
  // pagina con el numero de cuenta, que es un numero y se colaria como un importe.
  const cierres = anclas.filter((a) => a.esSaldo && /fermeture/.test(filas[a.indice].descripcion.join(' ')));
  const hasta = cierres.length ? cierres[cierres.length - 1].indice : Math.max(...anclas.map((a) => a.indice));

  // Reparto de las lineas de continuacion. Se hace por PROXIMIDAD EN VERTICAL a la linea principal,
  // y no por reparto del hueco entre dos filas: repartir el hueco deja lineas sin dueno o las da a
  // dos, y esa segunda forma fusionaba el tipo de una operacion con el de la siguiente
  // ("Contribution" + "Opérations Sur" + "Devises" acababan siendo un solo tipo inventado).
  // El interlineado dentro de una descripcion es de 3 a 7 puntos y el de dos operaciones seguidas de
  // 18, asi que la linea principal mas cercana es siempre la suya.
  const anclasValidas = anclas.filter((a) => a.indice <= hasta);
  const continuacion = new Map(anclasValidas.map((a) => [a.indice, []]));
  const indicesAncla = new Set(anclasValidas.map((a) => a.indice));
  for (let j = anclasValidas[0].indice; j <= hasta; j++) {
    if (indicesAncla.has(j)) continue;
    let mejor = anclasValidas[0];
    let distancia = Infinity;
    for (const a of anclasValidas) {
      const d = Math.abs(cuerpo[j].y - a.y);
      if (d < distancia) { distancia = d; mejor = a; }
    }
    continuacion.get(mejor.indice).push(j);
  }

  const operaciones = [];
  for (const a of anclasValidas) {
    const fila = filas[a.indice];
    // Las lineas del bloque, en orden de lectura (de arriba abajo), incluida la principal. Dentro de
    // una linea los fragmentos ya vienen ordenados por su x. Las lineas estan ordenadas de ARRIBA a
    // abajo por su coordenada y, asi que el orden de lectura es el indice ASCENDENTE.
    const delBloque = [a.indice, ...continuacion.get(a.indice)].sort((x, y) => x - y);

    const textos = [];
    const tipos = [];
    for (const j of delBloque) {
      const f = filas[j];
      textos.push(f.descripcion.join(' '));
      if (f.tipo.length) tipos.push(f.tipo.join(' '));
      // En una linea de continuacion no deberia haber importes, porque los importes van en la linea
      // principal. Si aparece uno, se avisa en vez de sumarlo dos veces o perderlo.
      if (j !== a.indice) {
        for (const n of ['cantidad', 'cadPrecio', 'cadBruto', 'cadComision', 'cadNeto',
          'usdPrecio', 'usdBruto', 'usdComision', 'usdNeto']) {
          if (f[n] !== null) errores.push(`${rel}: importe ${n}=${f[n]} en una linea de continuacion`);
        }
      }
    }
    const textoDescr = textos
      .map((s) => s.replace(/\|/g, ' ').replace(/\s+/g, ' ').trim())
      .filter(Boolean)
      .join(' ');

    const registro = {
      seccion,
      orden: a.indice,
      tipo: tipos.join(' ').replace(/\s+/g, ' ').trim(),
      simbolo: fila.simbolo.join(' ').replace(/\s+/g, ' ').trim(),
      descripcion: textoDescr,
      cantidad: fila.cantidad,
      cadPrecio: fila.cadPrecio, cadBruto: fila.cadBruto, cadComision: fila.cadComision, cadNeto: fila.cadNeto,
      usdPrecio: fila.usdPrecio, usdBruto: fila.usdBruto, usdComision: fila.usdComision, usdNeto: fila.usdNeto,
    };
    const tipo = registro.tipo;

    if (a.esSaldo) {
      operaciones.push({ clase: 'saldo', saldo: /fermeture/.test(textoDescr) ? 'cierre' : 'apertura', ...registro });
      continue;
    }
    if (!tipo) {
      errores.push(`${rel}: fila con fecha ${fila.fechaOperacion} y sin tipo: ${textoDescr.slice(0, 80)}`);
      continue;
    }
    operaciones.push({
      clase: 'operacion',
      fecha: fechasDe(fila.fechaOperacion),
      fechaLiquidacion: fechasDe(fila.fechaLiquidacion),
      ...registro,
    });
  }

  return { operaciones, errores };
}

function buscarPdf(dir, prefijo = '') {
  const out = [];
  for (const e of fs.readdirSync(dir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
    const rel = prefijo ? path.join(prefijo, e.name) : e.name;
    const completo = path.join(dir, e.name);
    if (e.isDirectory()) out.push(...buscarPdf(completo, rel));
    else if (/\.pdf$/i.test(e.name)) out.push({ completo, rel });
  }
  return out;
}

const MESES = {
  janvier: 1, fevrier: 2, mars: 3, avril: 4, mai: 5, juin: 6, juillet: 7,
  aout: 8, septembre: 9, octobre: 10, novembre: 11, decembre: 12,
};
const sinAcentos = (s) => s.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();

// El mes del estado. En la cabecera de la primera pagina viene como "Mois courant: 30 septembre, 2020"
// (dia primero) y en el pie como "septembre 30, 2020   Mois courant: $1,3048" (mes primero). Se aceptan
// las dos y ademas se contrasta con la fecha del nombre del fichero, que las dos deben decir lo mismo.
function mesDelEstado(items) {
  const texto = items.map((i) => i.str).join(' ');
  let m = texto.match(/Mois courant\s*:?\s*(\d{1,2})\s+([A-Za-z\u00c0-\u00ff]+)\s*,?\s*(\d{4})/);
  if (m && MESES[sinAcentos(m[2])]) return `${m[3]}-${String(MESES[sinAcentos(m[2])]).padStart(2, '0')}`;
  m = texto.match(/([A-Za-z\u00c0-\u00ff]+)\s+(\d{1,2}),?\s*(\d{4})\s+Mois courant/);
  if (m && MESES[sinAcentos(m[1])]) return `${m[3]}-${String(MESES[sinAcentos(m[1])]).padStart(2, '0')}`;
  return null;
}

function numeroDeCuenta(items) {
  const texto = items.map((i) => i.str).join(' ');
  const m = texto.match(/No de compte\s*:?\s*(\d{6,})/);
  return m ? m[1] : null;
}

const archivos = buscarPdf(entrada);
console.log(`Estados de cuenta encontrados: ${archivos.length}`);

const estados = [];
const erroresGlobales = [];

for (const { completo, rel } of archivos) {
  const doc = await getDocument({
    data: new Uint8Array(fs.readFileSync(completo)),
    useSystemFonts: false, disableFontFace: true, isEvalSupported: false,
    useWorkerFetch: false, verbosity: 0,
  }).promise;

  const paginas = [];
  for (let p = 1; p <= doc.numPages; p++) {
    const pagina = await doc.getPage(p);
    const tc = await pagina.getTextContent();
    paginas.push(tc.items
      .filter((i) => typeof i.str === 'string' && i.str.trim() !== '')
      .map((i) => ({ str: i.str, x: i.transform[4], y: i.transform[5], w: i.width || 0 })));
  }

  const mes = mesDelEstado(paginas[0]);
  const cuenta = numeroDeCuenta(paginas[0]);
  const errores = [];

  // El mes del nombre del fichero, para contrastar.
  const mNombre = rel.match(/(\d{4})-(\d{2})-\d{2}\.pdf$/);
  const mesNombre = mNombre ? `${mNombre[1]}-${mNombre[2]}` : null;
  if (mes !== mesNombre) {
    errores.push(`${rel}: el estado dice mes ${mes} y el fichero dice ${mesNombre}`);
  }

  const registros = [];
  let paginas04 = 0;
  for (let p = 1; p <= doc.numPages; p++) {
    const items = paginas[p - 1];
    const texto = items.map((i) => i.str).join(' ');
    if (!(texto.includes('Symbole') && texto.includes('Description') && /Date de r/.test(texto))) continue;
    const esSeccion05 = /RÉGLÉES APRS LA|OPÉRATIONS RÉGLÉES|Opérations non réglées/.test(texto);
    const seccion = esSeccion05 ? '05' : '04';
    if (seccion === '04') paginas04 += 1;
    const r = operacionesDePagina(items, `${rel} p${p}`, seccion);
    registros.push(...r.operaciones.map((o) => ({ ...o, pagina: p })));
    errores.push(...r.errores);
  }
  if (paginas04 !== 1) errores.push(`${rel}: ${paginas04} paginas de la seccion 04 (se esperaba 1)`);

  estados.push({ archivo: rel, cuenta, mes, registros });
  erroresGlobales.push(...errores);

  const de04 = registros.filter((o) => o.clase === 'operacion' && o.seccion === '04');
  const de05 = registros.filter((o) => o.clase === 'operacion' && o.seccion === '05');
  console.log(`${rel}  ${mes ?? 'SIN MES'}  mov04=${de04.length}  sinReglar05=${de05.length}  saldos=${registros.length - de04.length - de05.length}${errores.length ? '  ERRORES=' + errores.length : ''}`);
  if (typeof doc.destroy === 'function') await doc.destroy();
}

fs.mkdirSync(path.dirname(salida), { recursive: true });
fs.writeFileSync(salida, JSON.stringify({ estados, errores: erroresGlobales }, null, 2), 'utf8');

console.log('');
console.log(`JSON: ${salida}`);
if (erroresGlobales.length) {
  console.log('');
  console.log(`ERRORES (${erroresGlobales.length}):`);
  erroresGlobales.forEach((e) => console.log('  ' + e));
}
