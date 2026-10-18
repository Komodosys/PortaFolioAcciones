# 10 — Estadísticas para el administrador: qué pedir y qué se puede responder

> Análisis de negocio, no de implementación. El objetivo es decidir **qué informes hacen falta** y, sobre
> todo, **cuáles no se pueden construir todavía** — porque un informe que da una cifra falsa es peor que no
> tenerlo.

**Fecha:** 2026-10-18 · **Estado:** propuesta, pendiente de decisiones.

---

## 1. La pregunta de negocio, antes que la pantalla

Un informe no se pide por tenerlo: se pide para tomar una decisión. Las que aquí importan son cuatro, y cada
una pide un informe distinto:

| La pregunta que se hace | Quién la hace | Lo que hay que enseñar |
|---|---|---|
| «¿Voy mejor o peor que el mes pasado?» | cualquiera | **Ganancia del periodo**, separada de lo que se aportó |
| «¿Qué me está funcionando y qué no?» | cualquiera | **Rendimiento por instrumento**, con su peso en el total |
| «¿Qué cuenta rinde más?» | quien tiene varias | **Rendimiento por cartera**, y **por tipo** (REER contra CELI) |
| «¿Cuánto tengo, y de quién es?» | el administrador | **Resumen del patrimonio**, por persona y por tipo |

Y una que **no** está en el alcance del producto: *«¿cuánto voy a pagar de impuestos?»*. Eso exige el
tratamiento fiscal de cada retiro, y el producto es de **seguimiento**, no de planificación fiscal.

---

## 2. Lo que los datos de hoy permiten responder

Esta es la parte incómoda, y es la que evita semanas de trabajo perdido.

| Pregunta | ¿Se puede hoy? | Por qué |
|---|---|---|
| Ganancia **actual** por posición | **Sí** | Hay coste por lote y precio de hoy |
| Ganancia **actual** por cartera o por persona | **Sí** | Se suma, y la API ya lo calcula |
| Agrupar por **tipo de registro** (REER/CELI/no registrado) | **Sí** | Está en la cartera desde la Fase 8 |
| **Peso** de cada instrumento en el total | **Sí** | Cantidad × precio, sobre el total |
| **Reparto por moneda** | **Sí** | La moneda de cotización está en el instrumento |
| Ganancia **por periodo** | **No** | Ver abajo: no hay histórico de precios |
| Valor de la cartera **en el tiempo** | **No** | Hay una sola foto, del mismo día |
| Ganancia **realizada** (lo ya vendido) | **No** | No se guarda el precio de venta |
| Cuánto vino del **precio** y cuánto del **dólar** | **No** | Los tipos de cambio no se guardan |

### Los tres huecos, con las cifras reales de la base

```
Precios guardados:      19 filas, TODAS del 2026-10-16   -> un solo día
Fotos de cartera:        7 filas, TODAS del 2026-10-17   -> un solo día
Lotes (compras):        40 filas, del 2026-01-20 al 2026-08-31
```

**1. No hay histórico de precios.** La recogida guarda un cierre al día, así que el histórico **empieza
ahora**. Un informe «ganancia del último año» no se puede calcular: los precios de hace un año no están.

**2. No se guarda el precio de venta.** Una posición cerrada guarda **cuándo** se cerró
(`ClosedAtUtc`) y **cuánto había costado** (`CostBasisAtClose`), pero no **por cuánto se vendió**. Sin eso,
la ganancia realizada no se puede calcular — y no se puede deducir: se decidió al cerrar y se perdió.

**3. Los tipos de cambio se piden y se tiran.** Se usan para convertir a la moneda base y se guardan solo en
memoria. Una variación del patrimonio se puede medir, pero **no se puede descomponer**: «subió 3 %» sin
poder decir si fue el mercado o el dólar.

---

## 3. Lo que hay que arreglar ANTES de los informes

Tres cambios pequeños que hacen posible todo lo demás. Sin ellos, los informes existen pero mienten.

| # | Qué | Tamaño | Qué desbloquea |
|---|---|---|---|
| 1 | **Guardar el precio de venta** al cerrar una posición | pequeño | Ganancia realizada, y la tabla de «ya vendido» |
| 2 | **Guardar el tipo de cambio de cada día** que se usa | pequeño | Descomponer una variación: precio contra divisa |
| 3 | **Rellenar el histórico de precios hacia atrás** | medio | Ganancia por periodo desde el primer día |

El tercero merece explicación: la fuente ya usada (Yahoo) devuelve **cierres históricos**, no solo el de
hoy. Una pasada de una sola vez —una petición por instrumento, no una por día— daría años de historia a
partir de los cuales los informes de periodo funcionan **desde el primer día**, en vez de esperar meses a
que el histórico se llene solo.

**Y una advertencia sobre el detalle:** el histórico rellenado hacia atrás son **cierres de mercado**. La
*posición* que se tenía en el pasado no se puede reconstruir con la misma fiabilidad, porque una venta
parcial reduce los lotes y borra la cantidad que había antes. Por eso el informe de periodo tiene que
distinguir dos cosas (ver §4, C).

---

## 4. El catálogo de informes

Los ejes de filtro son los que has pedido, y son **combinables**:

**Persona** · **Tipo de registro** (REER / CELI / no registrado) · **Cartera** · **Instrumento** ·
**Periodo** (mes, trimestre, año, «desde el principio», rango libre) · **Moneda**

| # | Informe | Qué responde | Datos que necesita | ¿Hoy? |
|---|---|---|---|---|
| **A** | **Resumen del patrimonio** — valor, coste, ganancia, por persona y por tipo | «¿cuánto tengo y de quién es?» | Nada nuevo | **Sí** |
| **B** | **Evolución en el tiempo** — valor y coste, mes a mes | «¿cómo voy?» | Fotos diarias (se llenan solas) | En un mes |
| **C** | **Ganancia por periodo** — por cartera, tipo e instrumento | «¿cuánto he ganado este trimestre?» | Precios históricos (§3.3) | Tras §3.3 |
| **D** | **Rendimiento por instrumento** — ordenado, con su peso | «¿qué funciona?» | Nada nuevo | **Sí** |
| **E** | **Aportado contra ganado** — el desglose que de verdad importa | «¿esto es mérito mío o del mercado?» | Registro de movimientos | **No** |
| **F** | **Concentración** — peso por instrumento, sector y moneda | «¿estoy demasiado metido en una cosa?» | Sector (no se guarda) | Parcial |
| **G** | **Efecto divisa** — cuánto del resultado es tipo de cambio | «¿me afecta el dólar?» | Tipos guardados (§3.2) | Tras §3.2 |

### La trampa del informe C, y cómo resolverla

Una ganancia por periodo se calcula como `(precio_final − precio_inicial) × cantidad`. **Eso solo es
correcto si la cantidad no cambió en el periodo.** Si se compró o se vendió algo en medio, la cifra no es un
rendimiento: mezcla el mercado con la decisión de comprar.

Y la cantidad pasada no siempre se puede reconstruir. Así que el informe tiene que ser **honesto**, no
aproximado:

> **Si algún lote tiene fecha de compra dentro del periodo, la fila se marca** —«se movió en el periodo»— y
> se enseña **el precio**, no una ganancia que no lo es.

Eso se detecta sin datos nuevos: `Lot.PurchaseDate` está guardado. Es la diferencia entre un informe que se
puede defender y uno que hay que explicar cada vez.

---

## 5. Qué construiría, y en qué orden

| Orden | Qué | Por qué en ese orden |
|---|---|---|
| 1 | **§3.1 y §3.2** — precio de venta y tipos guardados | Son dos campos y desbloquean dos informes. Y cada día que pasa **se pierden más datos**: lo que no se guarda hoy no se puede recuperar |
| 2 | **A + D** — resumen y rendimiento por instrumento | Se pueden hacer **ya**, con lo que hay. Dan valor el primer día |
| 3 | **§3.3** — relleno del histórico | Convierte «espera un año» en «funciona hoy» |
| 4 | **B + C** — evolución y ganancia por periodo | Entonces sí, y con la marca de «se movió» |
| 5 | **G** — efecto divisa | Con los tipos guardados |
| 6 | **E** — aportado contra ganado | Es el mejor informe y el más caro: exige registrar movimientos |

**El 1 antes que nada** no es orden de preferencia: es que los datos que no se guardan hoy son historia que
se pierde. Lo demás se puede hacer cuando sea.

---

## 6. Decisiones que necesito

1. **¿El administrador ve todo junto o siempre filtrado?** Yo haría **las dos**: una vista «todo el mundo»
   para saber el total de la casa, y el filtro por persona a un clic. Pero si el uso real es «mirar el
   total», la vista por persona sobra.
2. **¿Basta el rendimiento del precio, o hace falta «aportado contra ganado»?** El segundo es lo que la
   gente quiere de verdad —«he puesto 40.000 y tengo 47.000»— y cuesta bastante más, porque exige registrar
   cada movimiento. El primero da «el precio subió un 8 %», que responde otra pregunta.
3. **¿Se exporta?** Pantalla, o además CSV para abrirlo en una hoja de cálculo. El CSV es barato y para dos
   personas que llevan su propia contabilidad suele ser lo que se acaba usando.
4. **¿Se compara con algo?** «8 % este año» no dice si es bueno. Contra un índice —el S&P/TSX, por
   ejemplo— sí. Es un salto de alcance: hay que descargar y guardar el índice igual que un instrumento.
5. **¿Cada cuánto se mira?** Si es una vez al mes, un informe mensual calculado al vuelo basta. Si es a
   diario, conviene guardar los totales ya sumados en vez de recalcularlos.

---

## 7. Lo que NO incluiría

- **Proyecciones y jubilación.** Es otro producto: exige supuestos de rentabilidad e inflación que aquí no
  se pueden justificar.
- **Comparación entre personas como si fuera una competición.** El administrador ve las carteras de todos
  por necesidad de administración, no para comparar a nadie. Un informe que ordene a los miembros de la
  familia por rendimiento sobra.
- **Rentabilidad ponderada por tiempo o por dinero (TWR / MWR).** Son las medidas correctas cuando hay
  aportaciones, y son la respuesta de verdad a «¿qué tal lo hago?» — pero exigen el registro de movimientos
  (§4.E) y son difíciles de explicar. Cuando exista el registro, se decide.
