# ADR-015 — El efectivo se lleva por moneda y se informa al cambio de hoy

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-20
- **Decide:** usuario (proyecto personal/familiar)

## Contexto

La cuenta reconstruida (RRSP ···126) es de **dos monedas**. No es un detalle que se pueda ignorar: **el dinero
entra en coronas canadienses y se convierte entero a dólares** el mismo día o el siguiente, diez veces en seis
años. Sumado de los estados de cuenta:

| | CAD | USD |
|---|---|---|
| Entra (aportaciones y traspasos) | 21.541,86 | 17.743,16 |
| Convierte (10 operaciones) | −21.541,86 | +16.324,93 |
| Compra, vende, cobra y paga | — | −84.876,76 |
| **Saldo** | **0,00** | **25,71** |

El modelo guardaba **una sola bolsa**: cada movimiento se apuntaba con su moneda y su tipo de cambio en el
momento, y el saldo era la suma de todos convertidos. Eso funciona mientras todo esté en una moneda; con dos,
**mezcla el dinero que entró en coronas con el que salió en dólares como si fueran lo mismo**. El resultado,
medido antes de esta decisión:

| | Valor |
|---|---|
| Saldo que daba la aplicación | **−1.050,39 CAD** (y el gráfico, **+2.269,20**: dos cifras distintas) |
| Saldo real, según el detalle del estado | **0,00 CAD + 25,71 USD** |
| Lo que informa el bróker, combinado al cambio del mes | **36,59 CAD** |

La cuenta no estaba «descuadrada por poco»: le sobraban unos **21.500 CAD** que ya no estaban —el CAD que entró y
nunca se apuntó que salió al convertirse—, y a la vez le faltaban los dólares que ese CAD produjo.

### El mecanismo, para que no haya que redescubrirlo

El saldo se calculaba como `apertura + aportes + traspasos + ventas + dividendos − comisiones − comprado`, cada
término **convertido al tipo de cambio de su propio día**. Con dos monedas eso es una suma de cosas que no son
del mismo tipo: el aporte en CAD y la compra en USD acaban en la misma columna sin nada que los relacione. La
conversión era justo ese eslabón, y **no estaba**: el cargador la descartaba con la etiqueta «no modelado».

## Opciones consideradas

1. **Seguir con una sola bolsa en la moneda base**, sumando cada flujo a su tipo. Es lo que había.
   *Ventajas:* una sola cifra, simple de leer, ya implementado.
   *Inconvenientes:* **mezcla monedas**. En esta cuenta da 21.500 CAD de dinero inexistente, y no hay manera de
   arreglarlo eligiendo mejor el tipo de cambio: se probó y la diferencia entre convenios era de 2.936 CAD en lo
   vendido, sin que ninguno cuadrara con el bróker.

2. **Una bolsa por moneda, y el total al cambio del día de la consulta.** *(Elegida.)*
   *Ventajas:* cada bolsa es **exacta** y se puede comprobar contra el estado (0,00 y 25,71); el total es el mismo
   criterio que usa el bróker cuando informa de un saldo combinado; desaparecen las dos cifras que se contradecían.
   *Inconvenientes:* el total en coronas **cambia cada día** sin que nadie haga nada; hay que decirlo; y hay más
   piezas: un tipo de movimiento nuevo, su migración y su sitio en la pantalla.

3. **Guardar el saldo calculado** y leerlo. *Descartada:* un saldo guardado puede dejar de coincidir con los
   movimientos que lo produjeron, y entonces hay dos verdades. Ya está escrito en el código del lector.

4. **Convertir cada bolsa al tipo de su último movimiento.** *Descartada:* el saldo de hoy quedaría valorado a un
   tipo de hace semanas, y cambiaría de valor cuando se registrara el siguiente movimiento. Un número que se mueve
   por un motivo que no es el suyo es peor que uno que se mueve por el que sí.

## Decisión

**El efectivo se lleva por moneda. El saldo es la suma de las bolsas, cada una al cambio del día en que se
consulta.** Y con tres obligaciones que forman parte de la decisión, no son detalles de implementación:

1. **Un cambio de moneda es un movimiento de dos patas.** Sale un importe en una moneda y entra otro en otra, y las
   dos son obligatorias: `Movement.Amount` es lo que sale y `CounterAmount` lo que entra. El tipo implícito **no se
   guarda**, se deduce de las dos patas, porque guardarlo sería una tercera cifra que puede contradecir a las dos.
   Un cambio de moneda **no cambia lo aportado ni el valor**; solo cambia en qué bolsa está.
2. **Una sola implementación del saldo.** La ficha, el gráfico y cualquier pantalla futura preguntan a
   `ICashReader`. Antes había dos cuentas —una en el lector y otra dentro del gráfico— y se separaban en **3.320
   CAD**. Dos maneras de calcular lo mismo acaban diciendo cosas distintas del mismo dinero.
3. **Obligación de etiquetado.** La cifra en coronas es una **conversión al cambio de hoy**, y la aplicación no
   puede presentarla como si fuera un saldo fijo. La cifra que no se mueve es la de la moneda en que está el
   dinero; la de coronas se mueve con el dólar y hay que decirlo al lado.

## Consecuencias

- **Positivas:**
  - **El saldo cuadra con el estado**: bolsa CAD **0,00** y bolsa USD **25,72**. La de coronas a cero es la prueba
    de que las diez conversiones eran lo que faltaba: todo el CAD que entró se convirtió.
  - **Desaparecen las dos cifras que se contradecían.** El gráfico usa el mismo lector, así que no puede discrepar
    de la ficha ni por un céntimo.
  - **El detalle se puede comprobar.** La pantalla enseña las dos bolsas con su importe y su cambio, no un número
    cerrado que hay que creerse.
  - **Extiende el criterio del [ADR-011](ADR-011-coste-al-cambio-de-hoy.md)**: allí el coste y el valor se
    convierten al cambio de hoy; aquí el saldo, igual. Un solo criterio de conversión en toda la aplicación.
- **Negativas / coste asumido:**
  - **La cifra en coronas cambia cada día** sin que nadie haga nada. Es la naturaleza de un saldo en dólares visto
    desde Canadá, pero obliga a etiquetarla y a comparar con el estado **al cambio de ese día**.
  - **El gráfico hace una consulta por punto** (60 puntos, 1,2 s). Es el precio de tener una sola implementación.
    Si algún día molesta, se optimiza sin cambiar el resultado.
  - **El alta de un cambio de moneda solo está en la API**, no en el formulario de movimientos. Cargar las diez de
    esta cuenta no necesitaba formulario; la siguiente vez, sí.
  - **Queda un residuo de 0,70 USD que NO es de la aplicación: está dentro del estado.** Su detalle, sumado línea a
    línea, da 25,71 USD y 0,00 CAD; su línea de efectivo dice 36,59 CAD, que al cambio del mes son 26,41 USD. Lo
    más probable es una operación pendiente de liquidar del apartado 05, que la extracción excluye a propósito
    porque reaparece al mes siguiente. **No se da por bueno hasta verlo en el estado de octubre.**
- **Lo que este ADR deja fuera, y necesita el suyo:** el informe de rendimiento **no calcula el efectivo: lo lee
  de la foto guardada del día**. Por eso siguió enseñando −1.050,39 después de arreglar el lector, hasta que se
  tomó una foto nueva. Es una decisión distinta —¿en vivo o de la foto?— y va en su propio ADR.
- **Condición de revisión:** (a) si el bróker empieza a informar los saldos **por moneda** en vez de combinados,
  porque entonces la obligación de etiquetado cambia de forma; (b) si aparece una **tercera moneda**, que el
  diseño ya soporta sin cambios —una bolsa más— pero conviene comprobarlo con una cuenta real antes de darlo por
  bueno; (c) si el residuo de 0,70 USD resulta no ser una operación pendiente, porque entonces habría algo en la
  extracción que este ADR no ha visto.
