# ADR-004 — Cobertura de mercados (EE. UU. + Canadá) con presupuesto cero

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

> **Base de la aceptación (2026-10-12):** el usuario eligió explícitamente la **opción A** después de
> conocer el riesgo, incluida la advertencia de que la fuente sin contrato cubrirá la mayor parte de
> la cartera porque la moneda base es CAD y el mercado principal es Canadá.

## Contexto

El usuario ha concretado:

- **Mercados:** Estados Unidos y Canadá, principalmente.
- **Moneda base:** **dólar canadiense (CAD)**, lo que confirma que el mercado canadiense es el
  principal y no el secundario.
- **Volumen:** entre 20 y 60 posiciones en total entre los portafolios de la familia.
- **Presupuesto de datos:** **cero**.
- **Renta fija:** solo fondos y ETF de renta fija (sin bonos individuales).
- **Precios:** cierre diario, una vez al día (ver [ADR-002](ADR-002-estrategia-de-precios-cierre-diario.md)).

El problema es que estas condiciones **no encajan de forma natural**: los planes gratuitos de los
proveedores de datos de mercado son, casi sin excepción, **solo de EE. UU.** Tiingo, Financial
Modeling Prep, Twelve Data y Finnhub cubren el mercado americano en su nivel gratuito; **Canadá (TSX
y TSXV) queda fuera** o exige un plan de pago. Esto no es una carencia técnica sino una decisión
comercial repetida del sector.

Y hay una consecuencia que conviene decir en voz alta: **como la moneda base es CAD y el mercado
principal es Canadá, la fuente sin contrato acabaría cubriendo la mayor parte de la cartera**, no la
parte accesoria. La arquitectura de "fuente estable para lo secundario, fuente frágil para el resto"
queda invertida respecto a lo habitual, y eso agrava el riesgo en lugar de mitigarlo.

## Opciones consideradas

1. **Fuente gratuita de EE. UU. + Yahoo Finance (no oficial) solo para Canadá.**
   - Ventajas: coste cero; cubre TSX (`.TO`) y TSXV (`.V`) con datos de cierre y 15 min de retraso;
     la cobertura de fondos canadienses es razonable.
   - Inconvenientes: Yahoo no ofrece contrato, soporte ni compromiso de estabilidad; sus términos de
     uso no autorizan el acceso programático; el endpoint puede cambiar sin aviso.
2. **Yahoo Finance (no oficial) como fuente principal de toda la cartera.**
   - Ventajas: una sola integración, cobertura completa, coste cero, y es la misma fuente que el
     usuario ya usa (los números coincidirán con lo que ve en Yahoo).
   - Inconvenientes: **un único punto de fallo sin contrato para todo el sistema**. Si Yahoo bloquea
     o cambia, la aplicación deja de tener precios de golpe.
3. **EODHD, plan EOD All-World (19,99 USD/mes).**
   - Ventajas: cubre EE. UU. y Canadá con contrato; incluye fondos, renta fija y tipos de cambio
     históricos; elimina el riesgo de la opción 2 de raíz.
   - Inconvenientes: 240 USD/año, contrario a la restricción de presupuesto cero declarada.
4. **Valoración manual para las posiciones canadienses.**
   - Ventajas: coste cero y control total; sin dependencia externa.
   - Inconvenientes: sostenible con 2–3 posiciones, insostenible con 15; el usuario tendría que
     buscar y teclear precios, que es justo lo que la aplicación debe evitar.

## Decisión

**Opción 1**, con el puerto de precios abstraído y una cadena de respaldo explícita, y con la
decisión de *proveedor* aplazada hasta comprobar la cobertura real de los instrumentos concretos.

- `IPriceProvider` es el puerto del dominio; habrá **al menos dos adaptadores** desde el primer día
  (uno gratuito de EE. UU. y uno para Canadá), y un `CompositePriceProvider` que enruta por
  instrumento.
- Cada instrumento guarda su `ProviderKey`, de modo que "qué fuente usa este instrumento" es un dato,
  no una regla escondida en el código.
- La **cadena de respaldo** (primario → secundario → último precio conocido marcado como obsoleto) se
  implementa desde el MVP, no cuando falle algo.

Se descarta la opción 2 por concentrar todo el riesgo en un proveedor sin contrato. Se descarta la
opción 3 por contradecir la restricción de presupuesto, pero queda **documentada como la salida
natural** si la opción 1 se vuelve insostenible. Se descarta la opción 4 salvo como válvula de escape
puntual para instrumentos sin cobertura en ninguna fuente.

## Consecuencias

- **Positivas:** arranque a coste cero; ninguna decisión de este ADR hipoteca el diseño, porque el
  cambio de proveedor es sustituir un adaptador; los dos adaptadores desde el día uno obligan a que
  la abstracción sea real y no decorativa.
- **Negativas / coste asumido:** la fiabilidad de **la mayor parte de la cartera** (las posiciones
  canadienses, que además están en la moneda base) depende de una fuente sin contrato. Hay que asumir
  que **algún día fallará** y que la respuesta será implementar el adaptador de EODHD, no improvisar.
  Es un coste diferido, no evitado. El usuario acepta este riesgo de forma consciente tras conocerlo.
- **Tercera vía a verificar antes de implementar:** Alpha Vantage incluye la bolsa de Toronto en su
  catálogo de instrumentos y su plan gratuito permite **25 peticiones al día**. Para ~20 posiciones
  canadienses con cierre diario podría bastar, pero no para el total de una cartera de 20–60. Merece
  una prueba real antes de descartarlo, aunque su límite diario lo hace poco holgado.
- **Riesgo legal/ético asumido:** el acceso programático a Yahoo Finance no está autorizado por sus
  términos de uso. Para un proyecto estrictamente personal y familiar, con 2–5 usuarios y sin
  redistribución de datos, el riesgo práctico es bajo, pero es una decisión consciente del usuario y
  queda registrada aquí.
- **Verificación de cobertura (2026-10-12): COMPLETA.** **19 de 19 instrumentos cubiertos** —los 18 de
  los estados de cuenta reales de la familia (2 titulares, 7 cuentas), más `VEEV`— con cero sin
  encontrar, cero sin precio, cero identidades dudosas y cero errores. **Incluye la categoría de
  riesgo: los 4 fondos mutuos canadienses** (TD Fixed Income, Canadian Equity, Global Equity y Tactical
  Pool, serie W) están cubiertos a cierre diario. **No hace falta contratar ningún plan de pago para
  valorar esta cartera.** Detalle en
  [../08-prueba-cobertura-instrumentos.md](../08-prueba-cobertura-instrumentos.md).
- **Coste oculto de los fondos, que no es dinero:** la equivalencia entre el código Fundserv del bróker
  y el símbolo interno de la fuente **no se puede automatizar** —la fuente no indexa por código
  Fundserv, y en Canadá estos fondos no tienen ISIN— y hay que resolverla **a mano una vez por
  instrumento**, confirmando que el NAV del candidato coincide con el precio del estado de cuenta. Es
  trabajo humano puntual por instrumento nuevo, no un coste recurrente.
- **Hallazgo colateral a favor:** la fuente gratuita entrega **eventos de splits** (`events=div,splits`),
  de modo que el tratamiento de acciones corporativas —pospuesto por suponer que requería un proveedor
  de pago— resulta viable sin coste adicional.
- **Condición de revisión:** (a) Yahoo deja de responder o bloquea; (b) la cartera incorpora mercados
  fuera de Norteamérica; (c) más de ~5 instrumentos quedan sin cobertura automática. Cualquiera de
  las tres reabre este ADR y muy probablemente lo sustituye por uno que contrate EODHD.
