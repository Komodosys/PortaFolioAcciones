// Carga de los datos reales desde los CSV del bróker.
//
// **Lee los ficheros, no lleva los datos dentro.** Y eso no es un detalle de estilo: los CSV tienen el
// nombre completo, los números de cuenta del bróker y el patrimonio de la familia, y el repositorio se
// publica. Si la cartera estuviera escrita aquí, se publicaría con él. Por eso este fichero solo sabe
// *cómo* se carga, y los datos se los da quien lo ejecuta.
//
// Qué hace, en orden:
//
//   1. Lee los CSV y construye un PLAN —personas, cuentas, instrumentos y posiciones— sin tocar nada.
//   2. Con `--simulacro`, lo enseña y se para. Es lo que hay que hacer la primera vez.
//   3. Si no, lo ejecuta: da de alta lo que falta y no toca lo que ya está.
//
// Uso:
//   node herramientas/cargar-datos/cargar.mjs --simulacro
//   node herramientas/cargar-datos/cargar.mjs
//
// Necesita el `.env` y que la API esté levantada.

import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { randomUUID } from 'node:crypto';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const RAIZ = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const API = process.env.API ?? 'http://localhost:8080';

const simulacro = process.argv.includes('--simulacro');

// ------------------------------------------------------------------------------------------------
// Leer los ficheros
// ------------------------------------------------------------------------------------------------

/** Lee un CSV con comillas, que es lo que hace falta: las descripciones pueden llevar comas. */
function leerCsv(ruta) {
  if (!existsSync(ruta)) {
    throw new Error(`No existe ${ruta}`);
  }

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

  return cuerpo.map((valores) =>
    Object.fromEntries(cabecera.map((nombre, indice) => [nombre.trim(), (valores[indice] ?? '').trim()])));
}

function leerEnv() {
  const ruta = join(RAIZ, '.env');

  if (!existsSync(ruta)) {
    throw new Error('No existe el .env: hace falta para entrar como administrador.');
  }

  const valores = {};

  for (const linea of readFileSync(ruta, 'utf8').split(/\r?\n/)) {
    if (linea.trim().startsWith('#') || !linea.includes('=')) { continue; }

    const corte = linea.indexOf('=');
    valores[linea.slice(0, corte).trim()] = linea.slice(corte + 1).trim();
  }

  return valores;
}

const listado = leerCsv(process.env.LISTADO ?? join(RAIZ, 'listado.csv'));
const lotes = leerCsv(process.env.LOTES ?? join(RAIZ, 'lotes.csv'));
const mapa = leerCsv(process.env.MAPA ?? join(RAIZ, 'herramientas', 'pdf-extract', 'mapa-instrumentos.csv'));
const entorno = leerEnv();

const claveDelAdmin = entorno.ADMIN_PASSWORD;

if (!claveDelAdmin) {
  throw new Error('Falta ADMIN_PASSWORD en el .env.');
}

// ------------------------------------------------------------------------------------------------
// Cliente de la API
// ------------------------------------------------------------------------------------------------

function cliente() {
  const galletas = new Map();
  let testigo = null;

  async function pedir(metodo, ruta, cuerpo) {
    const cabeceras = {};

    if (galletas.size > 0) {
      cabeceras.cookie = [...galletas].map(([k, v]) => `${k}=${v}`).join('; ');
    }

    if (cuerpo !== undefined) { cabeceras['content-type'] = 'application/json'; }
    if (testigo && metodo !== 'GET') { cabeceras['x-csrf-token'] = testigo; }

    const r = await fetch(`${API}${ruta}`, {
      method: metodo,
      headers: cabeceras,
      body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
    });

    for (const una of (r.headers.getSetCookie?.() ?? [])) {
      const [par] = una.split(';');
      const corte = par.indexOf('=');
      galletas.set(par.slice(0, corte), par.slice(corte + 1));
    }

    const texto = await r.text();
    let json = null;
    try { json = texto ? JSON.parse(texto) : null; } catch { /* puede no ser JSON */ }

    return { estado: r.status, json, texto };
  }

  async function entrar(usuario, clave) {
    // El testigo se pide ANTES y se manda en el propio inicio de sesion: sin el, la API responde
    // `csrf.invalid` — que es justo lo que tiene que hacer, porque si no, cualquier pagina podria iniciar
    // sesion en nombre de quien visita.
    const csrf = await pedir('GET', '/api/auth/csrf');
    testigo = csrf.json?.token ?? null;

    const r = await pedir('POST', '/api/auth/login', { userName: usuario, password: clave });
    testigo = r.json?.csrfToken ?? testigo;

    if (r.estado !== 200) {
      throw new Error(`No se pudo entrar como '${usuario}': ${r.estado} ${r.json?.title ?? ''}`);
    }

    return r.json;
  }

  return { pedir, entrar };
}

// ------------------------------------------------------------------------------------------------
// El plan
// ------------------------------------------------------------------------------------------------

/**
 * De qué clase es cada cosa y cómo se consigue su precio.
 *
 * El CSV del bróker dice el símbolo, el nombre y la moneda, y **nada más**. Qué clase de activo es y cómo
 * se obtiene su precio es una decisión, no un dato que venga en el estado de cuenta — y por eso se deduce
 * aquí, con una regla escrita, en vez de preguntar instrumento por instrumento.
 */
function comoSeValora(tipoDeActivo) {
  const esFondo = ['MutualFund', 'BondFund'].includes(tipoDeActivo);

  return esFondo
    ? { valuationMode: 'Nav', pricingFrequency: 'DailyNav' }
    : { valuationMode: 'Market', pricingFrequency: 'IntradayDelayed' };
}

/**
 * El tipo de cuenta del broker -> el tipo de registro del dominio.
 *
 * El estado de cuenta usa los nombres INGLESES (RRSP, TFSA). Y si algun día trae uno que no se conoce, se
 * dice «no registrado» en vez de inventarle una ventaja fiscal que quizá no tiene.
 */
function tipoDeRegistro(tipoCuenta) {
  const enMayusculas = (tipoCuenta ?? '').toUpperCase();

  if (enMayusculas.startsWith('RRSP')) {
    return 'RegisteredRetirement';
  }

  if (enMayusculas.startsWith('TFSA')) {
    return 'RegisteredSavings';
  }

  return 'NotRegistered';
}

/** El nombre de la cartera: el tipo de cuenta y los últimos caracteres, que bastan para distinguirlas. */
function nombreDeLaCartera(tipoCuenta, cuenta) {
  return `${tipoCuenta} ···${cuenta.slice(-3)}`;
}

/**
 * El coste por unidad que hay que guardar, y no es el que trae el estado.
 *
 * El estado da el coste unitario **redondeado a dos decimales** y, aparte, el total exacto. Multiplicar el
 * redondeado por las unidades no da el total: 110 × 115,77 son 12.734,70 y el estado dice 12.734,85. Esos
 * quince céntimos de diferencia, repetidos en cada posición, hacen que los totales no cuadren — y una
 * verificación contra el bróker que falla por céntimos es una verificación inútil.
 *
 * Se guarda el total dividido entre las unidades: lo que se conserva exacto es el total, que es el dato.
 */
function costePorUnidad(cantidad, costeTotal, costeUnitario) {
  if (!cantidad || !Number.isFinite(costeTotal) || costeTotal === 0) {
    return costeUnitario;
  }

  return costeTotal / cantidad;
}

/** Un nombre de entrada a partir del nombre de la persona, sin acentos y sin espacios. */
function nombreDeEntrada(titular) {
  return titular
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '.')
    .replace(/^\.|\.$/g, '')
    .slice(0, 40);
}

const plan = {
  personas: [],
  cuentas: [],
  instrumentos: new Map(),
  posiciones: [],
};

for (const fila of listado) {
  if (!fila.Simbolo || !fila.Cuenta) { continue; }

  const titular = fila.Titular;
  const cuenta = fila.Cuenta;
  const simbolo = fila.Simbolo;

  if (!plan.personas.some((p) => p.titular === titular)) {
    plan.personas.push({ titular, usuario: nombreDeEntrada(titular) });
  }

  const llaveCuenta = `${titular}|${cuenta}`;

  if (!plan.cuentas.some((c) => c.llave === llaveCuenta)) {
    plan.cuentas.push({
      llave: llaveCuenta,
      titular,
      tipoCuenta: fila.TipoCuenta,
      cuenta,
      nombre: nombreDeLaCartera(fila.TipoCuenta, cuenta),
    });
  }

  // El símbolo que se enseña es el del estado de cuenta; el que entiende la fuente, el propuesto.
  const delMapa = mapa.find((m) => m.Codigo === simbolo || m.Codigo === fila.SimboloPropuesto);

  plan.instrumentos.set(simbolo, {
    symbol: simbolo,
    name: fila.Descripcion || simbolo,
    quoteCurrency: fila.Moneda,
    providerKey: fila.SimboloPropuesto || delMapa?.YahooSymbol || null,
    brokerCode: fila.Simbolo,
    assetType: null,
    exchange: null,
  });

  plan.posiciones.push({
    titular,
    cuenta,
    simbolo,
    cantidad: Number(fila.Cantidad),
    costeUnitario: Number(fila.CosteUnitario),
    costeTotal: Number(fila.CosteTotal),
    moneda: fila.Moneda,
    fechaDelEstado: fila.FechaEstado,
  });
}

// ------------------------------------------------------------------------------------------------
// Ejecutar
// ------------------------------------------------------------------------------------------------

console.log();
console.log(simulacro ? '  SIMULACRO: no se va a escribir nada.' : '  Cargando de verdad.');
console.log();

const admin = cliente();
await admin.entrar('admin', claveDelAdmin);

// --- Los instrumentos: primero el catálogo, porque las posiciones los necesitan ---------------------
console.log('  Instrumentos');

const idDelInstrumento = new Map();

for (const [simbolo, datos] of plan.instrumentos) {
  const buscado = await admin.pedir('GET', `/api/instruments?q=${encodeURIComponent(simbolo)}`);
  const existente = (buscado.json ?? []).find((i) => i.symbol === simbolo);

  if (existente) {
    idDelInstrumento.set(simbolo, existente.id);
    console.log(`    ya estaba     ${simbolo}`);
    continue;
  }

  // La clase de activo y la bolsa se preguntan a la fuente, que es la que lo sabe.
  let deLaFuente = null;

  if (datos.providerKey) {
    const encontrado = await admin.pedir('GET', `/api/instruments/lookup?q=${encodeURIComponent(datos.providerKey)}`);
    deLaFuente = (encontrado.json ?? []).find(
      (m) => m.providerKey?.toUpperCase() === datos.providerKey.toUpperCase()) ?? (encontrado.json ?? [])[0] ?? null;
  }

  const tipoDeActivo = deLaFuente?.assetType ?? 'Equity';
  const comoSeValoraEsto = comoSeValora(tipoDeActivo);
  const bolsa = deLaFuente?.mic ?? (datos.providerKey?.endsWith('.TO') ? 'XTSE' : 'XNAS');

  if (simulacro) {
    console.log(`    se dará de alta ${simbolo.padEnd(12)} ${tipoDeActivo.padEnd(12)} ${bolsa.padEnd(6)} ${datos.providerKey ?? ''}`);
    continue;
  }

  const alta = await admin.pedir('POST', '/api/instruments', {
    symbol: datos.symbol,
    name: datos.name,
    assetType: tipoDeActivo,
    quoteCurrency: datos.quoteCurrency,
    exchange: bolsa,
    valuationMode: comoSeValoraEsto.valuationMode,
    pricingFrequency: comoSeValoraEsto.pricingFrequency,
    isin: null,
    country: null,
    providerKey: datos.providerKey,
    brokerCode: datos.brokerCode,
  });

  if (alta.estado >= 300) {
    console.log(`    ERROR         ${simbolo}: ${alta.estado} ${alta.json?.title ?? ''}`);
    continue;
  }

  idDelInstrumento.set(simbolo, alta.json.id);
  console.log(`    dado de alta ${simbolo.padEnd(12)} ${tipoDeActivo.padEnd(12)} ${bolsa.padEnd(6)} ${datos.providerKey ?? ''}`);
}

// --- Las personas ---------------------------------------------------------------------------------
// **Por pantalla se dice el NUMERO de persona, nunca su nombre ni su nombre de entrada.** Los nombres de
// entrada se derivan del nombre real, y una consola acaba copiada en un documento o en una conversacion:
// escribirlos aqui seria meter por la puerta de atras justo lo que este programa existe para no publicar.
// Las claves y los usuarios van al fichero de credenciales, que el repositorio ignora.
console.log();
console.log('  Cuentas de las personas');

const cuentasQueHay = await admin.pedir('GET', '/api/admin/users');
const existentes = new Map((cuentasQueHay.json ?? []).map((u) => [u.userName, u]));

const credenciales = [];
const clavePorPersona = new Map();
let numero = 0;

for (const persona of plan.personas) {
  numero++;

  if (existentes.has(persona.usuario)) {
    console.log(`    persona ${numero}: ya existia`);
    continue;
  }

  if (simulacro) {
    console.log(`    persona ${numero}: se creará`);
    continue;
  }

  // El alta EXIGE una contrasena: el administrador la elige. Aqui se pone una al azar que nadie llega a
  // usar —inmediatamente se sustituye por una temporal—, y eso evita el problema de elegir una «inicial»
  // comoda de adivinar que luego se queda puesta.
  const provisional = `${randomUUID()}-Aa1!`;

  const alta = await admin.pedir('POST', '/api/admin/users', {
    userName: persona.usuario,
    displayName: persona.titular,
    password: provisional,
    baseCurrency: 'CAD',
    language: 'es',
    isAdmin: false,
  });

  if (alta.estado >= 300) {
    console.log(`    persona ${numero}: ERROR ${alta.estado} ${alta.json?.title ?? ''}`);
    continue;
  }

  // Y ahora la temporal de verdad, que es la que se entrega en mano y obliga a cambiarla al entrar.
  const temporal = await admin.pedir('POST', `/api/admin/users/${alta.json.id}/reset-password`);

  if (temporal.estado >= 300) {
    console.log(`    persona ${numero}: creada, pero sin clave temporal (${temporal.estado})`);
    continue;
  }

  clavePorPersona.set(persona.titular, temporal.json.temporaryPassword);
  credenciales.push({
    usuario: persona.usuario,
    persona: persona.titular,
    clave: temporal.json.temporaryPassword,
  });

  console.log(`    persona ${numero}: creada`);
}

// --- El plan de las carteras, que se puede enseñar sin haber creado a nadie ------------------------
if (simulacro) {
  console.log();
  console.log('  Carteras y posiciones');

  numero = 0;

  for (const persona of plan.personas) {
    numero++;

    for (const cuenta of plan.cuentas.filter((c) => c.titular === persona.titular)) {
      const suyas = plan.posiciones.filter((p) => p.cuenta === cuenta.cuenta);

      console.log(`    persona ${numero} · ${cuenta.nombre}: ${suyas.length} posiciones`);

      for (const posicion of suyas) {
        const conLote = lotes.filter((l) => l.Cuenta === cuenta.cuenta && l.Simbolo === posicion.simbolo).length;

        console.log(`      ${posicion.simbolo.padEnd(12)} ${String(posicion.cantidad).padStart(10)} en ${conLote || 1} lote(s)`);
      }
    }
  }

  console.log();
  console.log('  SIMULACRO terminado. Nada se ha escrito.');
  console.log();
  process.exit(0);
}

// --- Las carteras y las posiciones, entrando como cada persona -------------------------------------
// Se entra como cada persona en vez de crear desde el administrador porque el alta de cartera crea para
// quien la pide: el administrador puede LEER y MODIFICAR datos ajenos, pero no hay forma de crear una
// cartera a nombre de otro. Entrar como cada uno usa el camino normal de la aplicación.
for (const persona of plan.personas) {
  const clave = clavePorPersona.get(persona.titular);

  if (!clave) {
    console.log();
    console.log(`  Persona ${plan.personas.indexOf(persona) + 1}: ya existia, sus carteras NO se han tocado.`);
    console.log('    (Para recargarlas hace falta su contraseña, no la temporal.)');
    continue;
  }

  const suyo = cliente();
  await suyo.entrar(persona.usuario, clave);

  const susCarteras = await suyo.pedir('GET', '/api/portfolios');
  const porNombre = new Map((susCarteras.json ?? []).map((c) => [c.name, c]));

  for (const cuenta of plan.cuentas.filter((c) => c.titular === persona.titular)) {
    let cartera = porNombre.get(cuenta.nombre);

    if (!cartera) {
      if (simulacro) {
        console.log(`    se creará la cartera ${cuenta.nombre}`);
        continue;
      }

      const alta = await suyo.pedir('POST', '/api/portfolios', {
        name: cuenta.nombre,
        // Obligatorio: la API no le pone un valor por defecto, para no etiquetar como no registrada una
        // cuenta de jubilación sin que nadie lo note.
        registrationType: tipoDeRegistro(cuenta.tipoCuenta),
        baseCurrency: 'CAD',
      });

      if (alta.estado >= 300) {
        console.log(`    ERROR cartera ${cuenta.nombre}: ${alta.estado} ${alta.json?.title ?? ''}`);
        continue;
      }

      cartera = alta.json;
      console.log(`    cartera ${cuenta.nombre} creada`);
    }

    // Las posiciones de esa cuenta.
    const vista = await suyo.pedir('GET', `/api/portfolios/${cartera.id}/view`);
    const yaPuestas = new Set((vista.json?.positions ?? []).map((p) => p.instrumentId));

    for (const posicion of plan.posiciones.filter((p) => p.cuenta === cuenta.cuenta && p.simbolo)) {
      const idInstrumento = idDelInstrumento.get(posicion.simbolo);

      if (!idInstrumento) { continue; }

      if (yaPuestas.has(idInstrumento)) {
        console.log(`      ${posicion.simbolo}: ya estaba`);
        continue;
      }

      // Los lotes de verdad, si los hay. Y si no, uno solo con la cantidad y el coste del estado, con la
      // fecha del estado marcada como ORIENTATIVA: no es la fecha de compra, es la única que se sabe.
      const susLotes = lotes.filter((l) => l.Cuenta === cuenta.cuenta && l.Simbolo === posicion.simbolo);

      const aEnviar = susLotes.length > 0
        ? susLotes.map((lote) => ({
            quantity: Number(lote.Cantidad),
            costPerUnit: costePorUnidad(Number(lote.Cantidad), Number(lote.CosteTotal), Number(lote.CosteUnitario)),
            purchaseDate: lote.Fecha || posicion.fechaDelEstado,
            dateAccuracy: lote.Fecha ? 'Exact' : 'Reference',
            notes: lote.Fecha ? null : 'Fecha orientativa: es la del estado, no la de la compra.',
          }))
        : [{
            quantity: posicion.cantidad,
            costPerUnit: costePorUnidad(posicion.cantidad, posicion.costeTotal, posicion.costeUnitario),
            purchaseDate: posicion.fechaDelEstado,
            dateAccuracy: 'Reference',
            notes: 'Agregado del estado: no se conoce el detalle de las compras.',
          }];

      const suma = aEnviar.reduce((t, lote) => t + lote.quantity, 0);

      if (Math.abs(suma - posicion.cantidad) > 0.0001) {
        console.log(`      AVISO ${posicion.simbolo}: los lotes suman ${suma} y el estado dice ${posicion.cantidad}`);
      }

      // Los lotes y el estado vienen de DOS extracciones distintas, y pueden no cuadrar al centimo. Cuando
      // pasa, manda el estado —que es el documento— y los lotes son su desglose. Si no se ajustara, cada
      // posicion con varios lotes se desviaria un centimo y los totales no cuadrarian nunca.
      const totalDeLosLotes = aEnviar.reduce((t, lote) => t + (lote.quantity * lote.costPerUnit), 0);
      const diferencia = posicion.costeTotal - totalDeLosLotes;

      if (Math.abs(diferencia) > 0.000001 && Math.abs(diferencia) <= 0.05 && aEnviar.length > 0) {
        const ultimo = aEnviar[aEnviar.length - 1];
        ultimo.costPerUnit += diferencia / ultimo.quantity;

        console.log(`      ajuste ${posicion.simbolo}: los lotes sumaban ${totalDeLosLotes.toFixed(2)} y el estado dice ${posicion.costeTotal.toFixed(2)}`);
      } else if (Math.abs(diferencia) > 0.05) {
        console.log(`      AVISO ${posicion.simbolo}: los lotes suman ${totalDeLosLotes.toFixed(2)} y el estado dice ${posicion.costeTotal.toFixed(2)} — diferencia demasiado grande para ser redondeo`);
      }

      if (simulacro) {
        console.log(`      se creará ${posicion.simbolo}: ${posicion.cantidad} en ${aEnviar.length} lote(s)`);
        continue;
      }

      const alta = await suyo.pedir('POST', `/api/portfolios/${cartera.id}/positions`, {
        instrumentId: idInstrumento,
        lots: aEnviar,
      });

      if (alta.estado >= 300) {
        console.log(`      ERROR ${posicion.simbolo}: ${alta.estado} ${alta.json?.title ?? ''}`);
        continue;
      }

      console.log(`      ${posicion.simbolo.padEnd(12)} ${posicion.cantidad} en ${aEnviar.length} lote(s)`);
    }
  }
}

// --- Las claves temporales, a un fichero y no a la pantalla ----------------------------------------
// La consola acaba copiada en un documento o en una conversacion. Las claves se escriben en un fichero que
// el repositorio ignora, y por pantalla solo se dice donde esta.
if (credenciales.length > 0 && !simulacro) {
  const ruta = join(RAIZ, 'credenciales-iniciales.txt');

  const texto = [
        'Claves temporales de las cuentas creadas por la carga de datos.',
        'Hay que cambiarlas en el primer acceso. Este fichero NO se publica.',
        '',
        'usuario;persona;clave temporal',
        ...credenciales.map((c) => `${c.usuario};${c.persona};${c.clave}`),
        '',
    ].join('\r\n');

  writeFileSync(ruta, texto, 'utf8');

  console.log();
  console.log(`  ${credenciales.length} clave(s) temporal(es) escrita(s) en:`);
  console.log(`    ${ruta}`);
  console.log('  No se enseñan por pantalla a propósito. Hay que dárselas en persona.');
}

console.log();
console.log(simulacro ? '  SIMULACRO terminado. Nada se ha escrito.' : '  Carga terminada.');
console.log();
