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

## 6. Decisiones tomadas (2026-10-18)

| # | Decisión | Respuesta | Consecuencia |
|---|---|---|---|
| 1 | Los huecos de datos | **Cerrar los tres**, incluido el relleno del histórico | Los informes de periodo funcionan desde el primer día |
| 2 | Qué significa «cuánto he ganado» | **Aportado contra ganado**, con registro de movimientos | Es la decisión que más cambia el proyecto: ver §7 |
| 3 | Salida | **Pantalla y CSV** | El CSV se genera desde los mismos datos, sin lógica duplicada |
| 4 | Referencia | **Índice configurable** | Hace falta guardar un índice y compararlo |
| 5 | Frecuencia de consulta | **Casi a diario** | Los totales diarios ya se guardan (las fotos): no hace falta nada nuevo para eso |
| 6 | Alcance del administrador | **Total de la casa y por persona** | Dos vistas sobre los mismos cálculos |

## 7. Lo que cambia con la decisión 2: el registro de movimientos pasa al centro

Las otras cinco decisiones son ajustes. **Esta no.** Pedir «aportado contra ganado» no es pedir un informe:
es pedir el **registro de cada movimiento** —aportaciones, compras, ventas y retiros, con su fecha y su
importe—, y ese registro es la base sobre la que se calculan casi todos los demás informes.

De hecho, con el registro:

| Informe | Antes (§2) | Con el registro |
|---|---|---|
| Ganancia **realizada** | **No** — no se guarda el precio de venta | **Sí**: una venta es un movimiento con precio |
| Ganancia **por periodo** | Solo si la cantidad no cambió | **Sí, siempre**: la cantidad en cada fecha se reconstruye |
| **Aportado contra ganado** | **No** | **Sí**, y es el informe central |
| Efecto **divisa** | Necesita guardar los tipos | Sí, y con más precisión |
| TWR / MWR | Fuera de alcance | **Posibles** |

Y desaparece la trampa del §4: la marca de «se movió en el periodo» deja de hacer falta, porque el registro
dice exactamente qué se movió y cuándo.

**El coste es que el proyecto crece.** Esto ya no es «añadir una pantalla de estadísticas»: es modelar los
movimientos, importarlos, validarlos y después construir los informes sobre ellos. Es una fase nueva, no un
trozo de una.

### La versión mínima que ya da el 80 %

El registro completo tiene cuatro tipos de movimiento. **Solo hacen falta dos para el informe central:**

| Movimiento | ¿Hace falta para «aportado contra ganado»? |
|---|---|
| **Aportación** (dinero que entra a una cuenta) | **Sí** — es el «aportado» |
| **Compra** | Sí, pero **ya está en los 40 lotes**, con fecha y precio |
| Venta | No: afecta a la ganancia realizada, que es otro informe |
| Retiro | No: es una aportación con el signo cambiado |

Es decir: **con aportaciones y lotes, el informe central funciona**, y los lotes ya existen. Las ventas
pueden llegar después sin rehacer nada, porque un registro de movimientos admite añadir tipos.

Eso convierte «meses» en algo mucho más razonable, y es por donde yo empezaría.

## 8. El plan, con estas decisiones

| Fase | Qué | Qué desbloquea | Tamaño |
|---|---|---|---|
| **1** | Precio de venta y tipos de cambio guardados | Nada visible, pero **deja de perderse información cada día** | pequeño |
| **2** | Relleno del histórico de precios | Serie de precios con años, no con días | medio |
| **3** | **Registro de movimientos: aportaciones y compras** | El informe central, y la base de todo lo demás | grande |
| **4** | **Aportado contra ganado**, por persona y por tipo | La pregunta que de verdad se hace | medio |
| **5** | Ganancia realizada y por periodo | Ya con el registro completo | medio |
| **6** | Rendimiento por instrumento, concentración y por moneda | «¿Qué funciona?» | medio |
| **7** | Índice de referencia configurable | «¿Es bueno el 8 %?» | medio |
| **8** | Exportación a CSV | Llevárselo a una hoja de cálculo | pequeño |

**La 1 va primero y no es negociable en el orden**: cada día que pasa sin guardar el precio de venta y el
tipo de cambio son datos que no se pueden recuperar. Todo lo demás se puede hacer cuando sea.

**La 4 es la que hay que enseñar cuanto antes.** Con la 3 hecha, es la que justifica todo el trabajo, y si
no convence, mejor saberlo antes de construir las otras cuatro.

## 9. Fase 1, hecha — y un hallazgo que cambia la fase

**Hecho: los tipos de cambio se guardan.** Antes se pedían, se usaban para convertir y se tiraban; el propio
código lo decía («no hay tabla de tipos de cambio»). Ahora hay una tabla `DailyFxRates` cuya clave es **el
par y el día**, así que pedir el mismo par dos veces el mismo día actualiza en vez de duplicar. Comprobado
contra la base: `USD/CAD = 1.4161` del 2026-10-16.

**Y el hallazgo: «el precio de venta» no es un campo, es un formulario.**

La fase 1 estaba descrita como «precio de venta y tipos de cambio guardados», como si el precio de venta
fuera un dato que la aplicación ya tuviera y solo hubiera que conservar. **No lo es.** La aplicación conoce
el **precio de mercado** del día, y eso no es a lo que se vendió: en una venta real el precio lo pone el
bróker, y casi nunca coincide con el último cierre.

Guardar el precio de mercado como «precio de venta» sería apuntar una cifra que suena exacta y no lo es, y
la ganancia realizada saldría mal por un margen que nadie podría explicar después.

**Consecuencia:** el precio de venta solo se puede guardar si **la persona lo escribe**, y eso es exactamente
un **movimiento** —fecha, cantidad y precio—. Es decir: **pertenece a la fase 3**, no a la fase 1.

Lo que sí se puede hacer sin preguntar nada, y queda hecho, es guardar **el precio de mercado en el momento
de cerrar**, etiquetado como tal. Es un dato honesto y sirve de aproximación declarada; la cifra exacta
llegará con el registro.


## 11. Fase 3, empezada: el modelo de movimientos

**La decisión que lo define: la compra ya existe.**

Un `Lot` tiene cantidad, coste por unidad y fecha de compra. Eso **es** una compra. Duplicarla en una tabla
de movimientos sería tener el mismo hecho en dos sitios, y el día que uno se corrigiera sin el otro dejarían
de cuadrar sin que nada fallara.

Así que la tabla `Movements` guarda **lo que hoy no existe**:

| Tipo | ¿Estaba? | Qué aporta |
|---|---|---|
| **Aportación** | No | El dinero que entra: el «cuánto he puesto» |
| **Retiro** | No | El dinero que sale |
| **Venta** | **No** | Cuántas unidades, a qué precio y qué día → la ganancia realizada |
| Compra | **Sí, como `Lot`** | No se duplica |

Y el «registro completo» de una cartera es **la unión de sus lotes y sus movimientos**: una consulta, no una
tabla copiada.

### Dos decisiones más, y las dos están en el código

1. **Una venta calcula su importe, no lo acepta.** Si se pudiera pasar, alguien podría registrar «10 unidades
   a 50» con un importe de 900, y la ganancia realizada saldría mal sin que nada lo delatara.
2. **Una venta no cambia lo aportado.** El dinero ya estaba dentro; solo cambió de forma. Es la prueba que
   más importa de este trozo, porque contarla como aportación haría que el informe dijera que se puso mucho
   más de lo que se puso.

### Lo que todavía hay que decidir: el efectivo

La aplicación **no tiene cuenta de efectivo**. Eso hoy no importa —está todo invertido—, pero convierte en
impreciso el informe central en cuanto haya dinero sin invertir o un retiro grande:

> Si se ingresan 10.000 y se compran 8.000, el patrimonio en valores vale 8.000. Sin registrar los 2.000 en
> efectivo, «puse 10.000 y tengo 8.000» diría que se perdió un 20 % cuando no se perdió nada.

Hay dos salidas, y es una decisión de producto, no técnica:

- **Registrar el efectivo** como parte de la cartera. Es el modelo completo y el informe sale exacto, pero
  añade un saldo que hay que mantener al día.
- **Definir el informe como «coste aportado contra valor de mercado»** —lo que la aplicación ya calcula— y
  llamarlo por su nombre, en vez de «aportado contra ganado». Es exacto y no hace falta nada nuevo, pero no
  responde «cuánto puse de mi bolsillo».

Mientras no se decida, lo construido sirve: el registro de ventas ya da la ganancia realizada, y las
aportaciones están guardadas aunque todavía no se use su saldo.
## 12. Lo que NO incluiría

- **Proyecciones y jubilación.** Es otro producto: exige supuestos de rentabilidad e inflación que aquí no se
  pueden justificar.
- **Comparación entre personas como si fuera una competición.** El administrador ve las carteras de todos
  por necesidad de administración, no para comparar a nadie. Un informe que ordene a los miembros de la
  familia por rendimiento sobra.
- **Rentabilidad ponderada por tiempo o por dinero (TWR / MWR).** Son las medidas correctas cuando hay
  aportaciones, y son la respuesta de verdad a «¿qué tal lo hago?» — pero exigen el registro de movimientos
  (§4.E) y son difíciles de explicar. Cuando exista el registro, se decide.
