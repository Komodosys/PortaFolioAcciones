# 05 — Estrategia de precios y tipos de cambio

> **Aviso de vigencia.** Los planes, precios y límites de los proveedores cambian con frecuencia y
> varios de los que aparecen aquí son artefactos generados por terceros, no páginas oficiales.
> Las cifras son orientativas (consultadas el **2026-10-12**) y **deben reconfirmarse en la web del
> proveedor antes de contratar nada**. Lo que no cambia con el tiempo es el *razonamiento* de este
> documento.

## 1. La decisión estructural: dos niveles, no uno

El problema de fondo es que en la misma cartera conviven activos que se valoran a frecuencias
distintas:

| Tipo | Cuándo tiene precio nuevo | Retraso típico | Se mueve durante la sesión |
|---|---|---|---|
| Acción / ETF de bolsa principal | Continuo durante la sesión | 15 min (gratis) o tiempo real (de pago) | Sí |
| Acción internacional | Continuo en **su** horario (y no coincide con el tuyo) | 15–20 min | Sí, pero en otra franja horaria |
| Fondo mutuo | **Una vez al día**, tras el cierre; a veces con un día de retraso | 1 día | **No** |
| Bono individual | Cotización de dealer, ilíquida | Variable o inexistente | No de forma fiable |
| Tipo de cambio | Continuo, pero el de referencia es diario | 1 día (referencia) | Sí en el mercado |

Intentar mostrar todo "en tiempo real" obliga a mentir en al menos dos filas de esa tabla.

**Recomendación — dos niveles:**

1. **Nivel base (obligatorio): cierre diario (EOD).** Una ejecución al día después del cierre de
   cada mercado. Es el mínimo común denominador de *todos* los tipos de activo, el más barato y el
   que hace coherentes las métricas: si todo se valora con cierres, la suma del portafolio es
   internamente consistente.
2. **Nivel superior (opcional, v0.2): precio intradía con 15 min de retraso**, solo para los
   instrumentos que lo tienen, solo durante la sesión, y **siempre con su marca de hora**. Nunca
   sustituye al cierre: es una capa de presentación encima.

Ver [ADR-002](decisiones/ADR-002-estrategia-de-precios-cierre-diario.md).

**Por qué esto importa más de lo que parece:** con cierre diario, un cálculo de G/P es reproducible
y auditable. Con una mezcla silenciosa de un precio de hace 15 minutos, un NAV de anteayer y un FX
de ayer, el total del portafolio cambia cada vez que se refresca la pantalla y deja de ser un
número en el que se pueda confiar. Para uso personal y familiar, **la coherencia vale más que la
frescura**.

## 2. Comparación de fuentes de datos

Comparo las opciones realistas a esta escala. "Cobertura" indica qué tipos de activo cubre de
verdad, no lo que anuncia el marketing.

| Fuente | Coste | Límites | Cobertura | Frecuencia / retraso | Fiabilidad | Notas |
|---|---|---|---|---|---|---|
| **Yahoo Finance (no oficial)** | Gratis, sin clave | Límites no publicados, cortes por IP | Acciones y ETF globales, fondos US, algunos bonos, FX | 15 min de retraso; EOD | **Baja-media**: sin contrato, puede romperse sin aviso | Endpoints no públicos. Uso no autorizado por sus términos. Aceptable como *fallback* en un proyecto personal; no como base del diseño |
| **EODHD** ([precios](https://eodhd.com/pricing)) | Gratis (sandbox) · **19,99 USD/mes** (EOD All-World) · 29,99 USD/mes (EOD + intradía) | Plan de pago: decenas de miles de llamadas/día | **La más amplia de las asequibles:** 70+ bolsas, fondos US, renta fija, FX histórico | EOD + intradía en el plan de 29,99 | Media-alta | La mejor relación cobertura/precio para una cartera con acciones internacionales y fondos |
| **Tiingo** ([planes](https://www.tiingo.com/about/pricing)) | Gratis (Power User, **solo uso no comercial**, atribución) · ~10 USD/mes (Commercial) | Tope por hora (cifra a verificar en su web) | Acciones y ETF US, fondos US, FX principales, noticias | EOD e IEX intradía | Media-alta | Muy buena relación calidad/precio en el plan gratuito para carteras US. Cobertura internacional limitada |
| **Financial Modeling Prep** ([análisis de precios](https://multiples.vc/compare/fmp-api-alternatives)) | Gratis (250 llamadas/día) · Starter ~22 USD/mes · Premium ~59 USD/mes | 250/día en gratis; tope de ancho de banda (500 MB gratis / 20 GB en el plan básico) | **Los planes baratos son solo EE. UU.**; la cobertura global está en el plan superior. Fondos sí, renta fija parcial | EOD en gratis; intradía en planes superiores | Media | Bueno para probar; el "tope de datos descargados" es una limitación que sorprende a quien solo mira el número de llamadas |
| **Twelve Data** ([planes](https://twelvedata.com/pricing)) | Gratis: 8 créditos/min y 800/día · Grow ~79 USD/mes · **Pro ~229 USD/mes** | Por **créditos**, no por llamadas: una petición con 50 símbolos puede consumir 50 créditos | Gratis: acciones y ETF **de EE. UU.**, FX, cripto. NAV de fondos y renta fija solo en el plan Pro | EOD global desde Grow | Media-alta | El modelo de créditos es la trampa a vigilar. El plan gratuito es útil solo para carteras US |
| **Alpha Vantage** ([soporte](https://www.alphavantage.co/support/), [premium](https://www.alphavantage.co/premium/)) | Gratis: **25 peticiones/día** · Premium desde 49,99 USD/mes (75/min) | 25/día y 5/min en gratis | Acciones, ETF, FX, cripto, macro. **Fondos mutuos: retirados** | EOD e intradía | Media | Su plan gratuito ya no sirve para nada serio. Descartable frente a las alternativas |
| **Finnhub** | Gratis (nivel de evaluación) · planes de pago aparte | Los límites publicados en fuentes de terceros no son fiables; **verificar** | Enfocado a acciones, sobre todo de EE. UU. Cobertura débil de fondos y renta fija | Tiempo real US en el plan gratuito | Media | Interesante por precio, poco útil para carteras con fondos y bonos |
| **Frankfurter** ([frankfurter.dev](https://frankfurter.dev/)) | **Gratis, sin clave**, uso comercial permitido | **Sin cuotas** (solo límite antiabuso) | Agrega **223 divisas de 104 bancos centrales y fuentes oficiales**, histórico desde 1948. Cubre CAD, USD y prácticamente cualquier divisa en que pueda estar una posición | Un valor diario; series temporales; filtro por proveedor (`providers=ecb`); salida CSV/NDJSON | Alta | **La mejor opción gratuita para FX, con diferencia.** Verificado contra su endpoint de divisas |

### Lectura de la tabla

- **Para FX, la decisión prácticamente se toma sola:** [Frankfurter](https://frankfurter.dev/) dejó
  de ser "el API de los tipos del BCE" y hoy agrega **223 divisas de 104 bancos centrales**, sin
  clave, sin cuotas y con histórico desde 1948. Para este proyecto el par que importa es **USD/CAD**,
  porque la moneda base es el dólar canadiense y las posiciones no canadienses están en USD: es el
  par más líquido y mejor documentado que existe, con histórico completo en la fuente gratuita. Si
  alguna vez entra una posición en otra divisa (EUR, MXN, GBP), también está cubierta sin coste.
  Detalle a tener en cuenta: por defecto devuelve una media agregada entre proveedores; cuando se
  necesite el tipo **oficial** de un banco central concreto, se filtra con `providers`.
- **Una ventaja inesperada de tener la base en CAD:** el cierre del TSX y el de la bolsa de Nueva
  York ocurren a la **misma hora** (16:00 ET) y el tipo USD/CAD de referencia se publica también al
  cierre. Precio y tipo de cambio son del mismo instante, así que la valoración diaria es coherente
  sin ningún ajuste. Con una base en euros y posiciones asiáticas, esto no sería cierto.
- **Para acciones y ETF, depende del mercado (pregunta B1).** Si todo es EE. UU., el plan gratuito
  de Tiingo o de FMP sobra. Si hay bolsas europeas o asiáticas, los planes gratuitos no llegan y
  EODHD a 19,99 USD/mes es el candidato más razonable.
- **Para fondos mutuos, el problema no es el precio: es la cobertura.** Es el punto donde más
  fuentes fallan (Alpha Vantage los retiró; varios planes baratos no los incluyen). Antes de elegir,
  hay que **probar los fondos concretos** contra la fuente candidata. Si un fondo no aparece en
  ninguna fuente asequible, la respuesta es valoración manual, no cambiar de proveedor.
- **Para bonos individuales, ninguna fuente asequible sirve.** Ver
  [01, matiz 3](01-inventario-funcionalidades.md).

## 3. ¿Cuánto se consume de verdad? (el cálculo que decide)

Con una cartera familiar realista: **~40–60 instrumentos distintos** (no posiciones: instrumentos
únicos, que es lo que se consulta).

| Escenario | Peticiones/día (sin agrupar) | Comentario |
|---|---|---|
| **EOD diario** | ~60 | Cabe en *cualquier* plan gratuito, incluso en los 250/día de FMP |
| **EOD + FX diario** | ~65 | Igual |
| **Intradía 15 min, sesión US (6,5 h → 26 refrescos)** | 26 × 60 = **~1.560** | Supera Twelve Data gratis (800/día) y arrasa Alpha Vantage gratis (25/día) |
| **Intradía 15 min con peticiones agrupadas** | ~26–100 **en teoría** | **En la práctica no aplica a la fuente gratuita:** su endpoint de cotizaciones por lotes responde 401, así que no hay forma de agrupar. Una petición por instrumento (comprobado, ver [08](08-prueba-cobertura-instrumentos.md)) |

**Conclusión cuantitativa:** el cierre diario cuesta, en términos de API, **veinticinco veces menos**
que el intradía. Para una cartera de largo plazo, el intradía no compra ninguna decisión mejor; solo
compra ansiedad. Esto, y no una preferencia estética, es lo que justifica el ADR-002.

Un matiz que hay que comprobar por proveedor: **una petición con 50 símbolos no siempre cuenta como
una llamada.** Twelve Data, por ejemplo, factura por créditos y varios proveedores cuentan un
crédito por símbolo. La diferencia entre "26 llamadas al día" y "1.560 créditos al día" es la
diferencia entre gratis y de pago.

## 4. Diseño del adaptador de precios

El punto clave de Clean Architecture aquí: **el dominio define qué necesita, no qué proveedor
usa.**

```
Domain/Pricing/IPriceProvider          ← puerto: "dame los precios de estos instrumentos"
Infrastructure/Pricing/EodhdPriceProvider      ← adaptador
Infrastructure/Pricing/TiingoPriceProvider     ← adaptador
Infrastructure/Pricing/ManualPriceProvider     ← adaptador (no hace red)
Infrastructure/Pricing/CompositePriceProvider  ← enruta por ValuationMode/AssetType
Infrastructure/Pricing/CachedPriceProvider     ← caché y control de cuota (decorador)
```

Detalles que evitan problemas reales:

- **Enrutado por `ValuationMode`**, no por tipo de activo: `Market` → proveedor de mercado;
  `Nav` → proveedor de fondos; `Manual` → el propio dato del usuario. Añadir un tipo nuevo no toca
  el núcleo.
- **`ProviderKey` por instrumento:** el código del instrumento en la fuente concreta, distinto del
  símbolo visible.
- **Cadena de respaldo explícita:** primario → secundario → último precio conocido (marcado como
  obsoleto). Nunca un cero ni un hueco silencioso.
- **Caché con TTL por frecuencia:** 15 min para intradía, hasta el siguiente cierre para EOD, 24 h
  para FX diario. Con eso, abrir la app diez veces no genera diez veces las llamadas.
- **Control de cuota y resiliencia:** reintentos con espera creciente, interruptor de circuito
  (Polly) y registro del consumo diario. Un plan gratuito se agota por un bucle, no por uso normal.
- **Registro de procedencia:** cada precio guarda fuente, fecha del dato y momento de obtención.
  Es lo que permite auditar por qué el total de ayer no coincide con el de hoy.
- **Fondos (`ValuationMode = Nav`): leer el último cierre no nulo de la serie, nunca el campo de
  "precio actual".** Comprobado: para un fondo mutuo la fuente puede devolver un precio anunciado
  como actual mientras el último punto de la serie diaria todavía es nulo, y ese precio es el NAV del
  día anterior. Tampoco se modela OHLC para un fondo: en la fuente, apertura, máximo, mínimo y cierre
  son idénticos entre sí y el volumen es cero. Un fondo tiene un valor, no una sesión.
- **Usar `close`, nunca `adjclose`, para valorar.** Comprobado: los dos campos difieren en todo
  instrumento que reparta dividendos, porque `adjclose` está ajustado retroactivamente. Para el valor
  de mercado se usa `close`; `adjclose` solo sirve para calcular rentabilidad total histórica.
  Confundirlos desplaza todas las valoraciones antiguas sin que nada falle.
- **Pedir los eventos corporativos** (`events=div,splits`). La fuente gratuita los entrega, y son lo
  que permite detectar un split que habría dejado la cantidad y el coste de una posición multiplicados.
- **Calendario de mercado por bolsa.** Comprobado en directo el 2026-10-12: era Acción de Gracias en
  Canadá, la TSX estaba cerrada y la NYSE abierta, así que las posiciones canadienses mostraban el
  cierre del viernes y las americanas el del lunes. Sin calendario por bolsa, la aplicación marcaría
  como «obsoleto» un precio que está perfectamente al día.
- **Nunca fallar en bloque:** si un instrumento no tiene precio, la posición se muestra sin valorar
  y el total se marca como parcial. Un error de la fuente no debe dejar la pantalla en blanco.

## 5. Tipos de cambio: histórico y actual

Se necesitan **los dos**, y para cosas distintas:

| Uso | Tipo de cambio | Por qué |
|---|---|---|
| Coste de la posición | **Histórico, de la fecha de compra** | Es el tipo al que realmente se adquirió el activo |
| Valor de mercado actual | **Actual (último cierre)** | Responde "¿cuánto vale hoy en mi moneda?" |
| Snapshots diarios | Cierre del día del snapshot | Coherencia con los precios de ese día |

Consecuencias de diseño:

1. **La fecha de compra deja de ser opcional** si se quiere una G/P consolidada correcta. Ver
   política en [04](04-modelo-de-datos.md).
2. **Se guarda el par tal como llega** (`USD/CAD`, no solo `EUR/USD` + `EUR/CAD`), para poder
   auditar el número y usar el par directo cuando exista.
3. **Toda conversión debe ser trazable:** qué par, de qué fecha, de qué fuente. Si el número no se
   puede explicar, no sirve.
4. **Los fines de semana y festivos no hay tipo nuevo**, y algunas fuentes publican con frecuencia
   semanal o mensual (Frankfurter las excluye de la media agregada y solo se consultan por
   proveedor). Para una compra en sábado, el tipo aplicable es el del último día hábil. Es correcto
   y hay que documentarlo, no parchearlo.
5. **Degradación explícita:** si no hay tipo para la fecha de compra dentro de la ventana aceptada,
   la posición no entra en el consolidado y se avisa. Nunca se usa el tipo de hoy en silencio.

## 6. Frescura del dato en la interfaz

Reglas de presentación que evitan la sensación de que la app "falla":

- Cada precio muestra **su fecha** ("cierre 10/10", "NAV 09/10", "15 min de retraso").
- Un indicador de **mercado abierto/cerrado** por posición, según su bolsa.
- Si el precio tiene más de X días, se marca como **obsoleto** (configurable).
- El total del portafolio indica **su hora de valoración** y si algún componente falta.
- Nunca se compara el "cambio del día" de un activo con precio de hoy contra uno con precio de
  ayer: eso produce porcentajes absurdos. Si las fechas difieren, el cambio del día se muestra como
  no disponible.

## 7. Cobertura real para este proyecto: EE. UU. + Canadá con presupuesto cero

El usuario ha concretado tres cosas, y dos de ellas **chocan entre sí**:

- Mercados: **EE. UU. y Canadá** principalmente.
- **Moneda base: dólar canadiense (CAD).** Esto confirma que el mercado canadiense es el principal,
  no el secundario.
- Volumen: **20–60 posiciones** entre todos los portafolios de la familia.
- Presupuesto de datos: **cero**.

Y una consecuencia que conviene decir sin rodeos: al ser el CAD la moneda base, **la fuente sin
contrato acabará cubriendo la mayor parte de la cartera**. La arquitectura habitual ("fuente estable
para casi todo, fuente frágil para las excepciones") queda invertida. Eso agrava el riesgo en lugar
de mitigarlo, y es la razón por la que esta decisión queda registrada con su riesgo explícito en
lugar de darse por resuelta.

**El problema:** casi todos los planes gratuitos son **solo de EE. UU.** Tiingo, FMP, Twelve Data y
Finnhub cubren el mercado americano en su nivel gratuito, y **Canadá (TSX/TSXV) queda fuera** o
requiere plan de pago. Esto no es una opinión: es la estructura comercial habitual de estos
proveedores, que reservan la cobertura internacional para los planes pagos.

Opciones, con su coste real:

| Opción | Coste | Cómo queda Canadá | Riesgo |
|---|---|---|---|
| **A. Fuente gratuita de EE. UU. + Yahoo Finance (no oficial) solo para Canadá** | 0 | Cubierto (TSX `.TO`, TSXV `.V`) | Yahoo no tiene contrato ni compromiso: puede cambiar o bloquear sin aviso. Los términos de uso no autorizan el acceso programático |
| **B. Yahoo Finance (no oficial) como fuente principal de todo** | 0 | Cubierto | Máxima comodidad y máxima fragilidad: un solo punto de fallo, sin contrato, sin soporte |
| **C. EODHD plan EOD All-World** | 19,99 USD/mes | Cubierto con contrato y SLA comercial | Ninguno relevante; el único coste es el dinero |
| **D. Comprar a mano los precios canadienses** | 0 | Valoración manual, actualizada por el usuario | Sostenible con 2–3 posiciones canadienses; inviable con 15 |

**Recomendación: A para el MVP, con el puerto de precios abstraído y preparado para C.**

Motivo: la opción A permite arrancar hoy a coste cero sin hipotecar el diseño. El día que Yahoo
falle, cambiar a EODHD es implementar un adaptador nuevo — precisamente lo que el
[ADR-001](decisiones/ADR-001-estructura-y-regla-de-dependencias.md) está pensado para facilitar. Lo
que **no** conviene es elegir B: depender de una única fuente sin contrato para toda la cartera
convierte una caída del proveedor en una caída de la aplicación.

Y una observación honesta sobre el presupuesto: si la cartera tiene una parte canadiense relevante,
19,99 USD/mes es el precio de dejar de preocuparse por esto. No es una recomendación de gasto, es el
dato que falta para decidir. Está registrado como [ADR-004](decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md).

**Sobre fondos mutuos canadienses:** era el punto más débil de todas las opciones, incluidas las de
pago. **Resuelto el 2026-10-12:** los cuatro fondos de la cartera real están cubiertos a cierre diario,
así que no hizo falta valoración manual. Pero con un matiz que conviene tener presente: **en Canadá un
fondo de Fundserv no tiene ISIN** y su código de fondo **no se indexa** en la fuente, de modo que la
equivalencia con el símbolo interno **se resuelve a mano una vez por instrumento**, confirmando el NAV
contra el estado de cuenta. Ver [08](08-prueba-cobertura-instrumentos.md).

## 8. Decisión recomendada (y su condición de revisión)

**Empezar con una fuente gratuita de EE. UU. más Yahoo no oficial para Canadá, FX por Frankfurter, y
el puerto de precios abstraído desde el primer día.**

Motivo: el coste de cambiar de proveedor es bajo *si* el adaptador está bien aislado, y el coste de
pagar cobertura que aún no se ha comprobado que haga falta es alto y recurrente. La secuencia
razonable es: probar los instrumentos reales contra las fuentes gratuitas → ver qué queda sin
cubrir → decidir si merece 19,99 USD/mes o si basta con valoración manual para las excepciones.

**Condición de revisión:** si Yahoo deja de funcionar para Canadá, o si la cartera incorpora mercados
fuera de Norteamérica, o si el número de posiciones sin cobertura pasa de un puñado, la decisión pasa
a ser "qué plan de pago", no "si pagar". Ese es el momento de cerrar
[ADR-004](decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md).
