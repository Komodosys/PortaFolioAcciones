// Fusiona listados de posiciones quedandose, por cuenta, con el estado mas reciente.
//
// Uso:  node fusionar.mjs <csv-nuevo> <csv-viejo> <csv-salida>
//
// Sirve para no perder las cuentas de las que solo hay estado antiguo cuando aparece informacion mas
// nueva de otras: por cada numero de cuenta se conserva el FechaEstado mayor.

import fs from 'node:fs';

const [rutaNuevo, rutaViejo, rutaSalida] = process.argv.slice(2);
if (!rutaNuevo || !rutaViejo || !rutaSalida) {
  console.error('Uso: node fusionar.mjs <csv-nuevo> <csv-viejo> <csv-salida>');
  process.exit(1);
}

function partirLinea(linea) {
  const campos = [];
  let actual = '';
  let entreComillas = false;
  for (let i = 0; i < linea.length; i++) {
    const c = linea[i];
    if (c === '"') {
      if (entreComillas && linea[i + 1] === '"') { actual += '"'; i++; }
      else entreComillas = !entreComillas;
    } else if (c === ',' && !entreComillas) { campos.push(actual); actual = ''; }
    else actual += c;
  }
  campos.push(actual);
  return campos;
}
function leerCsv(ruta) {
  const texto = fs.readFileSync(ruta, 'utf8').replace(/^\uFEFF/, '');
  const lineas = texto.split(/\r?\n/).filter((l) => l.trim());
  const cab = partirLinea(lineas[0]);
  return lineas.slice(1).map((l) => {
    const v = partirLinea(l);
    const o = {};
    cab.forEach((c, i) => { o[c] = v[i] ?? ''; });
    return o;
  });
}

const cab = ['Titular', 'Corredor', 'TipoCuenta', 'Cuenta', 'FechaEstado', 'Moneda', 'Simbolo',
  'Descripcion', 'SimboloPropuesto', 'Cantidad', 'Segregado', 'CosteUnitario', 'CosteTotal',
  'PrecioMercado', 'ValorMercado', 'Ganancia', 'PctRetorno', 'PctCartera'];

const porCuenta = new Map();
function anadir(filas, etiqueta) {
  const fechas = new Map();
  for (const f of filas) {
    const c = f.Cuenta;
    if (!fechas.has(c) || f.FechaEstado > fechas.get(c)) fechas.set(c, f.FechaEstado);
  }
  for (const f of filas) {
    const c = f.Cuenta;
    const actual = porCuenta.get(c);
    if (!actual || f.FechaEstado > actual.fecha) {
      porCuenta.set(c, { fecha: f.FechaEstado, origen: etiqueta, filas: filas.filter((x) => x.Cuenta === c) });
    }
  }
}

anadir(leerCsv(rutaNuevo), 'nuevo');
anadir(leerCsv(rutaViejo), 'viejo');

const resultado = [];
for (const [cuenta, datos] of [...porCuenta.entries()].sort()) {
  for (const f of datos.filas) {
    resultado.push({
      ...f,
      Segregado: f.Segregado || f.Cantidad,
    });
  }
  console.log(`  cuenta ${cuenta}: se usa el estado de ${datos.fecha} (origen: ${datos.origen}), ${datos.filas.length} posiciones`);
}

const out = [cab.join(',')];
for (const f of resultado) {
  out.push(cab.map((c) => {
    const s = String(f[c] ?? '');
    return /[",;\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  }).join(','));
}
fs.writeFileSync(rutaSalida, out.join('\n'), 'utf8');
console.log('');
console.log(`Total de posiciones: ${resultado.length}`);
console.log(`CSV: ${rutaSalida}`);
