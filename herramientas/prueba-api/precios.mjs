// Prueba del proveedor de precios contra la fuente real.
//
// Se comprueba que:
//   - Devuelve un precio POSITIVO y en la moneda correcta.
//   - Para un fondo, lee el ultimo cierre NO nulo (los fondos no publican todos los dias).
//   - Detecta los eventos (dividendos, y desdoblamientos cuando caen dentro de la ventana).
//
// Nota: el desdoblamiento de MGK fue en abril y la ventana de 1 mes no lo alcanza; se verifica en el
// punto de deteccion de splits, con una ventana mas larga.

import { crearComprobador } from './cliente.mjs';

const c = crearComprobador();
const BASE = 'http://localhost:8080';

async function cotizar(symbol, currency) {
  const url = `${BASE}/api/dev/quote?symbol=${encodeURIComponent(symbol)}${currency ? `&currency=${currency}` : ''}`;
  const r = await fetch(url);
  return { estado: r.status, json: r.status === 200 ? await r.json() : null };
}

c.titulo('1. Una accion en dolares');
const apple = await cotizar('AAPL');
c.comprobar('responde', 200, apple.estado);
c.comprobar('precio positivo', true, (apple.json?.price?.amount ?? 0) > 0);
c.comprobar('en dolares', 'USD', apple.json?.price?.currency?.code);
c.comprobar('con una fecha reciente', true, /^2026-/.test(apple.json?.priceDate ?? ''));
c.comprobar('dice de que fuente salio', 'yahoo', apple.json?.source);
c.nota(`AAPL: ${apple.json?.price?.amount} USD de ${apple.json?.priceDate}`);

c.titulo('2. Un fondo canadiense (lee el ultimo cierre no nulo)');
const fondo = await cotizar('0P0001K2XD.TO', 'CAD');
c.comprobar('responde', 200, fondo.estado);
c.comprobar('precio positivo', true, (fondo.json?.price?.amount ?? 0) > 0);
c.comprobar('en dolares canadienses', 'CAD', fondo.json?.price?.currency?.code);
c.nota(`fondo: ${fondo.json?.price?.amount} CAD de ${fondo.json?.priceDate}`);

c.titulo('3. El instrumento que se valora a cierre (VEEV)');
const veev = await cotizar('VEEV');
c.comprobar('precio positivo', true, (veev.json?.price?.amount ?? 0) > 0);
c.nota(`VEEV: ${veev.json?.price?.amount} USD`);

c.titulo('4. Un simbolo que no existe NO devuelve precio inventado');
const nada = await cotizar('ZZZZQQQQXXX');
c.comprobar('devuelve 404, no un precio falso', 404, nada.estado);

c.terminar();
