# ADR-002 — Precios: cierre diario como base, intradía como capa opcional

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

> **Base de la aceptación (2026-10-12):** el usuario fijó **presupuesto de datos cero**. Con ese
> límite, el intradía cada 15 minutos (~1.560 peticiones/día para 60 instrumentos) no cabe en ningún
> plan gratuito, mientras que el cierre diario sí. La decisión no se tomó por preferencia estética
> sino por restricción económica explícita.

## Contexto

La cartera mezcla activos con frecuencias de valoración distintas: acciones y ETF (precio continuo,
15 min de retraso con fuentes gratuitas), fondos mutuos (un NAV al día, a veces con un día de
retraso), bonos individuales (ilíquidos, sin precio fiable), y tipos de cambio (referencia diaria).

Además, el usuario ha planteado explícitamente la disyuntiva entre "precios con retraso de 15
minutos" y "cierre diario actualizado una vez al día", y ha pedido considerar cómo la frecuencia
afecta al coste de las fuentes, a la coherencia entre tipos de activos y a la experiencia de uso
esperada.

Datos que acotan la decisión (ver [05-precios-y-divisas.md](../05-precios-y-divisas.md)):

- Con ~60 instrumentos, el cierre diario consume ~60 peticiones/día; el intradía cada 15 minutos en
  la sesión de EE. UU., **~1.560/día** sin agrupar. Una diferencia de 25 veces.
- Los planes gratuitos van de 25 peticiones/día (Alpha Vantage) a 800 créditos/día (Twelve Data).
  El intradía los agota; el cierre diario no.
- **No existe ninguna fuente gratuita que dé intradía coherente para fondos y bonos**, porque no
  existe el dato: un fondo no tiene precio intradía.

## Opciones consideradas

1. **Solo intradía con 15 min de retraso.** Da la sensación de "app financiera". Obliga a pagar,
   agota cuotas, y **es imposible de sostener** para fondos y bonos: la tabla mostraría precios de
   hace 15 minutos junto a NAVs de anteayer sin que nada lo explique. El total del portafolio
   cambiaría cada vez que se refresca la pantalla.
2. **Solo cierre diario (EOD).** Todo se valora con el cierre del día. Coherente entre tipos de
   activo, barato, reproducible y auditable. La app se ve "quieta" durante la sesión.
3. **Cierre diario como base + intradía opcional como capa de presentación.** Lo mejor de cada uno:
   la valoración oficial es el cierre; el intradía es un extra explícito, con su marca de hora, que
   nunca sustituye al dato base.

## Decisión

**Opción 3, con la opción 2 como implementación del MVP.**

- El **cierre diario es la fuente de verdad** para valoración, totales, snapshots y métricas.
- Un **job diario** actualiza precios y tipos de cambio después del cierre de cada mercado.
- El **intradía (15 min de retraso) se pospone a v0.2** y, cuando llegue, será opcional por
  instrumento, solo durante la sesión, con su hora visible, y sin afectar a los snapshots.
- La valoración manual es un modo de primera clase para lo que ninguna fuente cubre.

## Consecuencias

- **Positivas:** el MVP cabe en cualquier plan gratuito; los totales son reproducibles y
  auditables; las métricas son coherentes entre acciones, fondos y bonos; el coste de datos se
  decide más tarde y con información, no ahora y a ciegas.
- **Negativas / coste asumido:** durante la sesión, la app no muestra movimientos en vivo. Es
  aceptable para seguimiento de largo plazo, pero es una expectativa que hay que fijar con el
  usuario desde el principio para que no se perciba como una carencia.
- **Condición de revisión:** si el usuario pide explícitamente seguimiento intradía y acepta pagar
  por ello, se implementa como capa opcional **sin cambiar el modelo de datos** (la tabla `Prices`
  sigue siendo la verdad). La decisión de *proveedor* se registra en un ADR aparte, que todavía no
  existe porque depende de saber qué mercados hay en la cartera.
