// La verificacion contra el broker: lo que la aplicacion dice tiene que ser lo que dice el estado de cuenta.
//
// Es la prueba que decide si la carga de datos sirvio. Y hay que tener claro QUE se puede comparar y que no:
//
//   * **Las cantidades y los costes SI**, y ademas tienen que coincidir EXACTOS: son hechos historicos y no
//     cambian porque pase el tiempo.
//   * **Los valores de mercado NO**: la aplicacion usa el precio de hoy y el estado es del 31 de agosto.
//     Compararlos como si debieran coincidir daria un fallo falso. Se enseñan al lado, como informacion.
//
// Lee `listado.csv`, que es la expectativa. **No lleva los datos dentro**: si los llevara, el repositorio se
// publicaria con el patrimonio de la familia.

import { readFileSync, existsSync } from 'node:fs';
import { crearCliente, crearComprobador, claveDelAdmin } from '../prueba-api/cliente.mjs';

const { comprobar, titulo, nota, terminar } = crearComprobador();

const RUTA = process.env.LISTADO ?? 'listado.csv';

if (!existsSync(RUTA)) {
  console.log(`  No existe ${RUTA}: sin el estado de cuenta no hay con que comparar.`);
  process.exit(1);
}

/** Lee un CSV con comillas. */
function leerCsv(ruta) {
  const texto = readFileSync(ruta, 'utf8').replace(/^\uFEFF/, '');
  const filas = [];
  let campo = '';
  let fila = [];
  let entreComillas = false;

  for (let i = 0; i < texto.length; i++) {
    const c = texto[i];

    if (entreComillas) {
      if (c === '"' && texto[i + 1] === '"') { campo += '"'; i++; }
      else if (c === '"') { entreComillas = false; }
      else { campo += c; }
      continue;
    }

    if (c === '"') { entreComillas = true; }
    else if (c === ',') { fila.push(campo); campo = ''; }
    else if (c === '\n') { fila.push(campo); filas.push(fila); fila = []; campo = ''; }
    else if (c !== '\r') { campo += c; }
  }

  if (campo !== '' || fila.length > 0) { fila.push(campo); filas.push(fila); }

  const [cabecera, ...cuerpo] = filas.filter((f) => f.length > 1 || (f.length === 1 && f[0] !== ''));

  return cuerpo.map((v) =>
    Object.fromEntries(cabecera.map((n, i) => [n.trim(), (v[i] ?? '').trim()])));
}

const estado = leerCsv(RUTA);

const admin = crearCliente('admin');
await admin.entrar('admin', claveDelAdmin());

const carteras = await admin.pedir('GET', '/api/portfolios');

/** El nombre que pone el cargador a la cartera de una cuenta. */
const nombreDe = (tipo, cuenta) => `${tipo} ···${cuenta.slice(-3)}`;

// ---------------------------------------------------------------------------------------------
titulo('1. Cada cuenta del estado existe en la aplicacion');

const cuentas = [...new Set(estado.map((f) => `${f.TipoCuenta}|${f.Cuenta}`))]
  .map((llave) => {
    const [tipo, cuenta] = llave.split('|');
    return { tipo, cuenta, nombre: nombreDe(tipo, cuenta) };
  });

for (const cuenta of cuentas) {
  const encontrada = (carteras.json ?? []).find((c) => c.name === cuenta.nombre);

  comprobar(`${cuenta.tipo} ···${cuenta.cuenta.slice(-3)} existe`, true, encontrada !== undefined);
  cuenta.id = encontrada?.id;
}

// ---------------------------------------------------------------------------------------------
titulo('2. Las CANTIDADES coinciden exactas');

let posicionesComparadas = 0;
let malLasCantidades = 0;

for (const cuenta of cuentas.filter((c) => c.id)) {
  const vista = await admin.pedir('GET', `/api/portfolios/${cuenta.id}/view`);
  const enLaApp = new Map((vista.json?.positions ?? []).map((p) => [p.symbol, p]));

  const esperadas = estado.filter((f) => f.Cuenta === cuenta.cuenta);

  for (const esperada of esperadas) {
    const real = enLaApp.get(esperada.Simbolo);
    posicionesComparadas++;

    const iguales = real !== undefined && Math.abs(real.quantity - Number(esperada.Cantidad)) < 0.0000001;

    if (!iguales) { malLasCantidades++; }

    comprobar(
      `${cuenta.tipo} ${esperada.Simbolo}: ${esperada.Cantidad}`,
      Number(esperada.Cantidad),
      real?.quantity ?? 'no esta');
  }
}

nota(`${posicionesComparadas} posiciones comparadas`);

// ---------------------------------------------------------------------------------------------
titulo('3. Los COSTES coinciden, con el total del estado y no con el unitario redondeado');

// El estado da el coste unitario redondeado a dos decimales y el total exacto. Si se guardara el unitario,
// cada posicion se desviaria unos centimos y los totales no cuadrarian.
let malLosCostes = 0;

for (const cuenta of cuentas.filter((c) => c.id)) {
  const vista = await admin.pedir('GET', `/api/portfolios/${cuenta.id}/view`);
  const enLaApp = new Map((vista.json?.positions ?? []).map((p) => [p.symbol, p]));

  for (const esperada of estado.filter((f) => f.Cuenta === cuenta.cuenta)) {
    const real = enLaApp.get(esperada.Simbolo);
    const esperado = Number(esperada.CosteTotal);
    const obtenido = real?.cost;

    // Medio centimo de margen: la base guarda cuatro decimales y el estado dos.
    const iguales = obtenido !== undefined && Math.abs(obtenido - esperado) <= 0.005;

    if (!iguales) { malLosCostes++; }

    comprobar(`${cuenta.tipo} ${esperada.Simbolo}: coste ${esperado}`, true, iguales);
  }
}

// ---------------------------------------------------------------------------------------------
titulo('4. El total de cada cuenta es la suma del estado');

for (const cuenta of cuentas.filter((c) => c.id)) {
  const vista = await admin.pedir('GET', `/api/portfolios/${cuenta.id}/view`);
  const enLaApp = new Map((vista.json?.positions ?? []).map((p) => [p.symbol, p]));

  const esperado = estado
    .filter((f) => f.Cuenta === cuenta.cuenta)
    .reduce((t, f) => t + Number(f.CosteTotal), 0);

  const obtenido = [...enLaApp.values()].reduce((t, p) => t + p.cost, 0);

  const iguales = Math.abs(obtenido - esperado) <= 0.02;

  comprobar(`${cuenta.tipo} ···${cuenta.cuenta.slice(-3)}: coste total ${esperado.toFixed(2)}`, true, iguales);

  if (!iguales) { nota(`  la app dice ${obtenido.toFixed(2)}`); }
}

// ---------------------------------------------------------------------------------------------
titulo('5. Y la aritmetica de la aplicacion es coherente consigo misma');

// Esto no depende del estado: es que el valor de una posicion sea de verdad cantidad por precio, y que la
// ganancia sea valor menos coste. Si esto falla, el problema no es de los datos cargados.
for (const cuenta of cuentas.filter((c) => c.id)) {
  const vista = await admin.pedir('GET', `/api/portfolios/${cuenta.id}/view`);

  for (const posicion of vista.json?.positions ?? []) {
    const valor = posicion.quantity * posicion.priceAmount;

    comprobar(
      `${posicion.symbol}: valor = cantidad × precio`,
      Math.round(valor * 100) / 100,
      Math.round(posicion.marketValue * 100) / 100);

    comprobar(
      `${posicion.symbol}: ganancia = valor − coste`,
      Math.round((posicion.marketValue - posicion.cost) * 100) / 100,
      Math.round(posicion.localGain * 100) / 100);
  }
}

// ---------------------------------------------------------------------------------------------
titulo('6. Los VALORES de mercado, solo como información');

// No se comparan: la aplicacion usa el precio de hoy y el estado es del 31 de agosto. Enseñar la diferencia
// sirve para saber cuanto se ha movido, no para aprobar o suspender la carga.
let valorDelEstado = 0;
let valorDeHoy = 0;

for (const cuenta of cuentas.filter((c) => c.id)) {
  const vista = await admin.pedir('GET', `/api/portfolios/${cuenta.id}/view`);

  valorDelEstado += estado
    .filter((f) => f.Cuenta === cuenta.cuenta)
    .reduce((t, f) => t + Number(f.ValorMercado), 0);

  // Se usa `totalValue`, que es el valor de la cartera en SU moneda base.
  valorDeHoy += (vista.json?.totalValue ?? 0);
}

nota(`valor en el estado (agregado, monedas mezcladas): ${valorDelEstado.toFixed(2)}`);
nota(`valor hoy en moneda base de cada cartera: ${valorDeHoy.toFixed(2)}`);
nota('no se comparan: son fechas distintas y monedas distintas');

// ---------------------------------------------------------------------------------------------
console.log();
console.log(malLasCantidades === 0 && malLosCostes === 0
  ? `RESULTADO: los totales cuadran con el estado (${posicionesComparadas} posiciones).`
  : `RESULTADO: ${malLasCantidades} cantidad(es) y ${malLosCostes} coste(s) no cuadran.`);

process.exit(malLasCantidades === 0 && malLosCostes === 0 ? 0 : 1);
