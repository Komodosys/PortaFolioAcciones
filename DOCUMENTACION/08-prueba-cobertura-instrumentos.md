# 08 — Prueba de cobertura de instrumentos reales

## Por qué esta es la siguiente acción

[ADR-004](decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md) aceptó cubrir Canadá con
una fuente sin contrato y dejó explícitamente pendiente una cosa: **verificar la cobertura real de
los instrumentos concretos**. Hasta tener esa respuesta no se sabe si el MVP es "todo se actualiza
solo" o "la mitad se teclea a mano", y esa diferencia se nota más en el uso diario que cualquier
decisión de arquitectura ya cerrada.

## Qué decide esta prueba

1. Si el MVP puede dar por hecho el precio automático o si la **valoración manual** debe ser una
   función de primera clase, visible en la interfaz y no un rincón oculto.
2. Si hay que **reabrir ADR-004** y plantearse el plan de pago.

## Reconocimiento previo (2026-10-12)

Antes de escribir la herramienta comprobé con instrumentos representativos qué devuelve realmente la
fuente candidata. Estos son resultados observados, no supuestos:

| Caso probado | Símbolo | Resultado observado |
|---|---|---|
| Acción EE. UU. | `AAPL` | Cubierto. USD, Nasdaq, `EQUITY`, historial diario, cierre del día |
| Acción Canadá | `RY.TO` | Cubierto. CAD, Toronto, `EQUITY`, historial diario |
| ETF Canadá | `XIU.TO` | Cubierto. CAD, Toronto, `ETF` |
| ETF renta fija EE. UU. | `AGG` | Cubierto. USD, NYSEArca, `ETF` |
| ETF renta fija Canadá | `ZAG.TO` | Cubierto. CAD, Toronto, `ETF` |
| Fondo mutuo EE. UU. | `VFIAX` | Cubierto como `MUTUALFUND`, pero con dos trampas (ver abajo) |
| Fondo mutuo Canadá | `0P0001HFBC.TO` | Cubierto: CAD, `MUTUALFUND`, NAV diario e historial de un mes |
| Búsqueda por ISIN | `US0378331005` → `AAPL` | Funciona **para acciones y ETF**. No sirve para fondos canadienses: no tienen ISIN |
| Cotización por lotes | `v7/finance/quote` | **401 Unauthorized**: no hay agrupación de peticiones |

### Hallazgo 1 — Los fondos mutuos canadienses están cubiertos, pero su símbolo es un código opaco

El fondo «TD Canadian Index Fund-O Series» existe en la fuente con el símbolo **`0P0001HFBC.TO`**, un
código de estilo Morningstar. Y el nombre corto que devuelve la fuente es **literalmente ese código**;
el nombre legible solo aparece en el campo largo.

Dos consecuencias directas sobre el diseño:

- **La búsqueda de instrumentos por nombre deja de ser un adorno y pasa a ser requisito del MVP.**
  Nadie puede teclear `0P0001HFBC.TO` de memoria.
  *(Matiz posterior, tras la verificación con la cartera real: **en Canadá los fondos de Fundserv no
  tienen ISIN** y su código de fondo no se indexa. La búsqueda por ISIN funciona para acciones y ETF,
  pero **no resuelve los fondos**. Ver el hallazgo 1 más abajo.)*
- **Hay que guardar nuestro propio nombre para mostrar.** El `shortName` de la fuente no sirve para
  un fondo. Esto confirma el campo `Name` propio en `Instruments` que ya preveía
  [04-modelo-de-datos.md](04-modelo-de-datos.md).

### Hallazgo 2 — Nunca confiar en el precio "actual" que anuncia la fuente para un fondo

En `VFIAX`, la fuente devolvía un `regularMarketPrice` con valor **mientras el último punto de la
serie diaria venía nulo**: ese precio era el NAV del día anterior. Un sistema que lea el campo
"precio actual" y lo muestre como si fuera de hoy estaría dando un dato un día viejo sin avisar.

**Regla para el diseño:** con `ValuationMode = Nav`, leer el **último cierre no nulo** de la serie y
usar **su fecha**. Es exactamente el problema que motivó exigir el «as of» visible por posición, ahora
con evidencia en lugar de con una corazonada.

### Hallazgo 3 — Los fondos no tienen intradía, y la fuente lo dice explícitamente

Para `VFIAX` y para el fondo canadiense, la lista de rangos válidos que devuelve la fuente **excluye
`1d` y `5d`**: solo ofrece `1mo`, `3mo`, `6mo`, `ytd`, `1y`, `2y`, `5y`, `10y` y `max`. No es una
limitación de nuestra integración; es que el dato no existe.

Esto es evidencia dura a favor del [ADR-002](decisiones/ADR-002-estrategia-de-precios-cierre-diario.md):
cualquier diseño que prometiera intradía para toda la cartera sería incapaz de cumplirlo para los
fondos.

Además, en un fondo los campos `open`, `high`, `low` y `close` son **idénticos entre sí** y el volumen
es cero: un fondo tiene un valor, no una sesión. Conviene no modelar OHLC para `ValuationMode = Nav`.

### Hallazgo 4 — No hay cotizaciones agrupadas

El endpoint de cotizaciones por lotes responde **401**. Es decir: **una petición por instrumento**, sin
alivio posible por agrupar símbolos.

Recalculando el consumo con esto:

| Escenario | Peticiones/día (60 instrumentos) | Cabe en un plan gratuito |
|---|---|---|
| Cierre diario | ~60 | Sí, en cualquiera |
| Intradía cada 15 min (sesión EE. UU.) | ~1.560 | **No, en ninguno** |

Refuerza la decisión ya tomada, ahora con el dato de que no existe la válvula de escape del *batching*
que habría suavizado el coste.

### Lo que el reconocimiento confirma del alcance ya decidido

Haber limitado la renta fija a **fondos y ETF** ([ADR-004](decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md),
respuesta A1) resulta ser la decisión afortunada: `AGG` y `ZAG.TO` se cubren como cualquier ETF,
mientras que un bono individual no tendría precio utilizable en ninguna fuente asequible.

## Qué comprueba la herramienta

Está en [../herramientas/prueba-cobertura/](../herramientas/prueba-cobertura/) y no necesita clave de
API. Para cada instrumento comprueba:

1. si el símbolo se resuelve, y **cómo** (directo, por ISIN o por nombre);
2. si el nombre que devuelve la fuente **coincide con el esperado** — detección de instrumento
   equivocado, que es el peor fallo posible porque no rompe nada, solo produce cifras falsas;
3. moneda, bolsa y tipo de instrumento según la fuente;
4. la **fecha real** del último cierre, no la que la fuente anuncia como actual;
5. si hay historial diario y si ese instrumento admite intradía.

Las instrucciones de uso están en
[../herramientas/prueba-cobertura/README.md](../herramientas/prueba-cobertura/README.md).

## Criterio de decisión

Fijado **antes** de ver los resultados, para no reinterpretarlos a conveniencia después:

| Instrumentos sin cubrir o con identidad dudosa | Lectura | Acción |
|---|---|---|
| 0–2 | El MVP es casi todo automático | Seguir; la valoración manual es la excepción |
| 3–5 | Cubierto con costuras | Seguir, pero con la valoración manual como función visible en la interfaz |
| Más de 5 | La fuente no sostiene el MVP | Reabrir [ADR-004](decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md) y valorar EODHD (19,99 USD/mes) |

## Verificación con la cartera real (2026-10-12)

La lista de instrumentos ya no es algo escrito a mano: sale de **8 estados de cuenta** reales (7
cuentas de Questrade y TD, 2 titulares), extraídos y validados contra los totales de los propios
estados. Ver [`listado.csv`](../listado.csv).

Resultado completo en
[../herramientas/prueba-cobertura/resultados/resumen.md](../herramientas/prueba-cobertura/resultados/resumen.md):

**Cobertura: 19 de 19** — los 18 instrumentos de los estados de cuenta, más `VEEV`, que estaba en la
lista previa pero **no aparece en ningún estado**. Cero sin encontrar, cero sin precio, cero
identidades dudosas, cero errores. **Incluida la categoría de riesgo: los 4 fondos mutuos canadienses
están cubiertos.**

Según el criterio fijado de antemano (0–2 sin cubrir → el MVP es casi todo automático),
[ADR-004](decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md) **queda verificado y no
hace falta ningún plan de pago** para valorar esta cartera a cierre diario.

### Hallazgo 1 — En Canadá un fondo de Fundserv no tiene ISIN, y su código tampoco sirve para buscar

El código Fundserv (`TDB2624C`) es lo que aparece en el extracto del bróker, pero **buscarlo en la
fuente devuelve cero resultados**. La fuente indexa los fondos canadienses por un código interno de
estilo Morningstar (`0P0001K312.TO`), y además su nombre corto es ese mismo código.

Consecuencia: **la equivalencia «código del bróker → identificador de la fuente» no se puede
automatizar; hay que resolverla una vez por instrumento y guardarla.** Es el campo `ProviderKey` que ya
preveía [04-modelo-de-datos.md](04-modelo-de-datos.md), y que pasa de precaución teórica a requisito de
la primera versión: sin él, los fondos no se valoran.

### Hallazgo 2 — Series con nombres casi idénticos y precios muy distintos

Al buscar «TD Canadian Equity Pool» aparecen dos candidatos: `0P0001K312.TO` (**Series W**) y
`0P0001K313.TO` (**Class Series W**), con NAV 22,68 y 28,01. Elegir por parecido de nombre es una
trampa. Lo mismo ocurre con el Global Equity Pool (`0P0001K314.TO` frente a `0P0001K315.TO`).

**Procedimiento que hay que incorporar a la aplicación**, y que es el que se usó aquí: buscar por
nombre y **aceptar el candidato solo si su NAV coincide con el precio que imprime el estado de cuenta**.

| Código Fundserv | Símbolo elegido | NAV del 2026-01-30 | Precio en el estado |
|---|---|---|---|
| TDB2560C | `0P0001K2XD.TO` | 9,180 | 9,180 ✅ |
| TDB2624C | `0P0001K312.TO` | 19,840 | 19,840 ✅ |
| TDB2625C | `0P0001K314.TO` | 21,360 | 21,360 ✅ |
| TDB2626C | `0P0001TKCZ.TO` | 13,830 | 13,830 ✅ |

Coinciden **al tercer decimal**: es una verificación objetiva, no una impresión.

### Hallazgo 3 — Los instrumentos se renombran

`MGK` figura en el estado como «VANGUARD MEGA CAP GROWTH ETF» y la fuente lo llama «Vanguard
Morningstar Mega Cap Growth ETF». Es el mismo instrumento. **La comprobación de identidad debe avisar,
no bloquear**, y debe comparar por palabras significativas y no por cadena exacta.

### Tres hallazgos anteriores que siguen vigentes

1. **El calendario de mercado por bolsa es obligatorio, no un detalle.** El 2026-10-12 es el segundo
   lunes de octubre, **Acción de Gracias en Canadá**: la TSX estaba cerrada y la NYSE abierta. Las
   posiciones canadienses mostraban el cierre del viernes 9 y las americanas el del lunes 12. Sin
   calendario por bolsa, la aplicación habría marcado las canadienses como «precio obsoleto» en un día
   perfectamente normal. El umbral de 4 días absorbe bien un fin de semana largo.
2. **Los splits son un problema real, y ya está resuelto a coste cero.** Netflix tuvo un split 10:1 el
   2025-11-17, declarado por la propia fuente en su bloque de eventos corporativos
   (`events=div,splits`). Una posición de NFLX introducida con datos anteriores al split tiene sus
   cifras multiplicadas por diez. La buena noticia: **el tratamiento automático de splits deja de
   necesitar un proveedor de pago.** La herramienta ya los consulta y los reporta.
3. **`close` y `adjclose` no son intercambiables.** Difieren en todo instrumento que reparta
   dividendos, porque `adjclose` está ajustado retroactivamente. Para valorar una posición hay que usar
   `close`; `adjclose` solo sirve para rentabilidad total histórica.

## Estado

- [x] Reconocimiento de la fuente con instrumentos representativos
- [x] Herramienta de prueba escrita, documentada y verificada su ejecución hasta el diagnóstico previo
- [x] **Cartera real completa: 19 de 19 cubiertos**, incluidos los 4 fondos mutuos canadienses
- [x] **[ADR-004](decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md) verificado**: no hace falta plan de pago
- [ ] Repetir la resolución a mano cuando se añada un fondo de otra gestora (el procedimiento está arriba)
- [ ] Probar la **estabilidad en el tiempo**, que ninguna prueba de una tarde puede demostrar

### Requisitos de entorno detectados

Dos obstáculos reales, y ninguno es un defecto del script:

1. **Política de ejecución de PowerShell.** Windows bloquea los scripts sin firma digital
   («is not digitally signed / cannot be loaded»). **Comprobado: cambiar la política del usuario a
   `RemoteSigned` no basta.** Con `CurrentUser = RemoteSigned`, el archivo sin firma y sin marca de
   descarga, la invocación directa `.\Test-Cobertura.ps1` sigue fallando. Lo que sí funciona es
   `-ExecutionPolicy Bypass` **en la invocación**, que solo afecta a ese proceso. Por eso se añadió el
   lanzador [ejecutar.cmd](../herramientas/prueba-cobertura/ejecutar.cmd), que lo aplica.
   `MachinePolicy` y `UserPolicy` están sin definir, así que no hay directiva de grupo de por medio.

2. **TLS en Windows PowerShell 5.1.** El shell por defecto de este equipo es Windows PowerShell 5.1
   (edición Desktop), que usa .NET Framework: la conexión TCP a la fuente funciona pero el handshake
   TLS falla. Con PowerShell 7 no ocurre.

El script ahora **avisa** en lugar de bloquear cuando detecta PowerShell 5.1, y hace una **petición de
prueba** antes de empezar: un fallo de entorno se comunica una vez con diagnóstico, en lugar de
producir decenas de filas de error que parecerían un problema de cobertura.

### Estado de la herramienta

- Sintaxis validada.
- **Ejecución verificada hasta el diagnóstico previo**: el script carga, resuelve sus rutas, lee el
  CSV y, ante un fallo de conexión, informa del entorno y termina con código 1 sin generar un informe
  engañoso. Comprobado lanzándolo como lo hará el lanzador
  (`powershell -ExecutionPolicy Bypass -File ...`).
- La recogida de datos propiamente dicha sigue pendiente de ejecutarse en un entorno con TLS
  funcional: PowerShell 7, o Windows PowerShell 5.1 fuera del contexto confinado.
- **Dos fallos detectados al probarla, ya corregidos:** `$PSScriptRoot` llegaba vacío en los valores
  por defecto de `param()` cuando el script se lanza con `-File`, lo que rompía el lanzador; y el
  bloqueo por versión de PowerShell era demasiado rígido, ahora es solo un aviso. Ninguno de los dos
  se habría visto sin ejecutar la herramienta.

## Cambios en la documentación que ya ha provocado

- La búsqueda de instrumentos **por nombre** pasa a ser **requisito del MVP**, con la equivalencia
  «código del bróker → símbolo de la fuente» resuelta a mano y guardada por instrumento
  ([02-priorizacion-mvp.md](02-priorizacion-mvp.md)).
- Queda fijada la regla de leer el último cierre no nulo para los fondos, y de no modelar OHLC para
  `ValuationMode = Nav` ([05-precios-y-divisas.md](05-precios-y-divisas.md)).
- Se confirma, con evidencia, que no hay cotizaciones agrupadas.
