# 11 — El efectivo en las carteras: plan para integrarlo

> **Decisión tomada:** hay registro de efectivo. Este documento es el **plan** para integrarlo, no la
> implementación. Termina con las cuatro decisiones que hacen falta para empezar.

**Fecha:** 2026-10-18 · **Estado:** plan, pendiente de decisiones.

---

## 1. El problema, en una frase

Hoy «lo que vale una cartera» son sus posiciones. Si hay dinero sin invertir, ese dinero **no existe** para
la aplicación: se ingresan 10.000, se compran 8.000, y el informe dice que se perdieron 2.000.

## 2. La trampa que hay que ver ANTES de escribir código

El efectivo no es un número. Un ejemplo del caso real:

> Una cuenta **en dólares canadienses** compra 10 acciones de **VEEV**, que cotiza **en dólares
> estadounidenses**. El bróker cobra CAD; la posición vale USD.

Para saber cuánto bajó el efectivo hay que convertir, y **al cambio de ese día**, no al de hoy: el dinero salió
de la cuenta el día de la compra.

Y aquí está el problema: **la aplicación no guarda el tipo de cambio de las compras**. El ADR-011 decidió a
propósito que el coste se convierte **al cambio de hoy**, precisamente para no depender de datos históricos de
divisa. Eso está bien para valorar una posición y **no sirve para llevar una cuenta de efectivo**, porque el
saldo de una cuenta no cambia cuando se mueve el dólar.

**Consecuencia:** un saldo «derivado» de los costes actuales daría un efectivo que **sube y baja solo** cada
día. Es incoherente y nadie entendería por qué tiene 300 CAD menos sin haber hecho nada.

## 3. Lo que sí se puede hacer sin reescribir el pasado

La buena noticia es que **no hace falta redefinir nada**. `PortfolioSnapshot.MarketValue` sigue significando
«lo que valen las posiciones», y el efectivo entra como un **campo nuevo**:

```
PortfolioSnapshot
  ├─ MarketValue   (ya existe)  lo que valen las posiciones
  ├─ CashValue     (nuevo)      el dinero en la cuenta
  ├─ CostBasis     (ya existe)  lo que costaron las posiciones
  └─ TotalValue    (calculado)  MarketValue + CashValue
```

Así **las fotos que ya existen siguen siendo válidas** —`CashValue` será cero hasta que se declare— y no hay
un corte en el histórico: el día que se declare el saldo inicial, el total sube de golpe y **se explica solo**.

La alternativa —redefinir `MarketValue` para que incluya el efectivo— haría que las fotos antiguas dijeran una
cosa y las nuevas otra **bajo el mismo nombre**. Es el tipo de cambio que nadie detecta hasta que un gráfico
da un salto raro.

## 4. El diseño propuesto: apertura declarada + movimientos

**El saldo no se deriva: se declara y se ajusta.** El pasado no se puede reconstruir —de las 40 compras que
hay no se sabe cuántos dólares canadienses salieron de la cuenta— así que se empieza por un **saldo inicial
declarado** y a partir de ahí lo mueve cada movimiento.

```
CashLedger (una fila por movimiento de efectivo)
  ├─ PortfolioId
  ├─ Kind        Opening | Deposit | Withdrawal  (las compras y ventas entran solas)
  ├─ OccurredOn
  ├─ Amount      en la moneda BASE de la cartera
  ├─ RateUsed    el tipo de cambio aplicado, si hubo conversión   ← auditable
  └─ Note

Saldo = Σ Amount  (la apertura es una fila más, no un caso especial)
```

**Las compras y las ventas no se teclean: entran solas.** Una compra ya es un `Lot`, y una venta ya es un
`Movement`. Lo que falta es **su efecto en el efectivo**, que se añade al registrarlas:

- **Compra**: `− (cantidad × coste por unidad) × tipo del día de la compra`
- **Venta**: `+ (cantidad × precio) × tipo del día de la venta`

Y el **tipo usado se guarda**, para poder decir de dónde sale cada cifra. Sin eso, dentro de un año nadie
sabría por qué el saldo es el que es.

## 5. Prerrequisito: los tipos de cambio hacia atrás

Los tipos de cambio empezaron a guardarse **ayer** (`DailyFxRates`, 2026-10-16). Para calcular el efecto de
una compra de enero hace falta el tipo **de enero**.

La fuente que ya se usa (Frankfurter) **publica series históricas**, así que es el mismo trabajo que ya se
hizo con los precios: **una petición por par y rango**, no una por día. Es un prerrequisito, no un extra: sin
él, el efectivo de las compras viejas saldría mal.

**Y hay una salida si se prefiere no hacerlo:** que la persona teclee **cuánto pagó en su moneda** al dar de
alta una compra. Es un dato más por compra, y el bróker lo da en el estado de cuenta. Con eso no hace falta
ningún histórico de divisa.

## 6. Qué cambia en lo que ya existe

| Sitio | Qué cambia | Riesgo |
|---|---|---|
| **La foto diaria** | Campo nuevo `CashValue`; `TotalValue` calculado | Ninguno: lo existente no se redefine |
| **La pantalla de la cartera** | Una línea de «efectivo» junto al total | Bajo |
| **El informe** | Tres columnas: posiciones, efectivo y total | Medio: hay que decidir cómo se presenta |
| **«Aportado contra ganado»** | **Ahora sí se puede calcular bien**: aportado = Σ aperturas y depósitos − retiros; ganado = total − aportado | Es lo que se buscaba |
| **La recogida diaria** | Toma el saldo al hacer la foto | Bajo |
| **Los movimientos** | Compra y venta generan su fila de efectivo | Medio: es donde se toca la lógica que ya funciona |

## 7. Lo que NO incluiría

- **Una cuenta de efectivo por divisa.** Es más exacto para quien tiene cuentas en varias monedas, pero
  convierte el saldo en una tabla de dos dimensiones y no es el caso de esta casa.
- **Intereses ni comisiones.** Un bróker cobra comisiones y a veces paga intereses. Registrarlos es otra
  funcionalidad; si aparecen, se añaden como un tipo más de movimiento, sin rehacer nada.
- **Conciliación automática con el bróker.** No hay conexión con el bróker; los datos se teclean o se cargan
  de un CSV.

## 8. Las cuatro decisiones que hacen falta

### 1. ¿Cómo se calcula el efecto de una compra en el efectivo?

- **La aplicación convierte con el tipo del día** y lo guarda. Menos trabajo al registrar, y el tipo queda
  auditable. **← recomendado**
- **La persona teclea cuánto pagó** en su moneda. Exacto siempre, y un campo más por compra.

### 2. ¿Una sola moneda o una por divisa?

- **Una, la de la cartera** (CAD). Coincide con la realidad de una cuenta canadiense y con el resto de la
  aplicación. **← recomendado**
- **Una por divisa.** Más exacto si de verdad se mantienen saldos en varias, pero multiplica la complejidad
  de todo lo que venga después.

### 3. ¿Qué se hace con las 40 compras que ya existen?

- **Un saldo inicial declarado** y a partir de ahí, limpio. **← recomendado**: el pasado no se puede
  reconstruir.
- **Reconstruirlo** convirtiendo cada compra con el tipo de su fecha. Más trabajo y el resultado sigue siendo
  una estimación: se ignora lo que hubiera en la cuenta antes.

### 4. ¿Dónde se ve y se corrige el saldo?

- **Una pantalla propia** dentro de la cartera, junto a los movimientos. **← recomendado**
- **Solo en el informe**, sin poder corregirlo. Si el saldo se descuadra, no habría forma de arreglarlo.

---


## 10. Decisiones tomadas (2026-10-18)

| # | Pregunta | Decisión | Consecuencia |
|---|---|---|---|
| 1 | El efecto de una compra | **La aplicación convierte y guarda el tipo** | Hace falta el histórico de divisa (trozo 1). El tipo queda auditable |
| 2 | Moneda del saldo | **Una, la de la cartera** — y la interfaz puede **enseñar el equivalente en USD al cambio de hoy**, solo informativo | Un saldo, no una tabla de dos dimensiones. La vista en USD no se guarda: es presentación |
| 3 | Las compras existentes | **Un saldo inicial declarado** | No se reconstruye el pasado; se dice dónde se empieza |
| 4 | Dónde se ve y se corrige | **Pantalla propia en la cartera** | Si el saldo se descuadra, hay dónde arreglarlo |

### Lo que se hizo al bajar la 1 y la 3 a código

**No hace falta un segundo importe.** Guardar el importe ya convertido *y* el tipo sería tener dos verdades
que pueden divergir. Se guarda **el tipo** (`Movement.RateUsed`) y el importe en la moneda de la cartera se
**deriva** de él con `AmountIn(baseCurrency)`.

**Y un fallo real que apareció al probarlo:** convertir multiplicando conserva la moneda de origen, así que
`500 USD × 1,4` daba **«700 USD»** donde son **700 CAD** — un número correcto con la etiqueta equivocada, que
es la peor forma de equivocarse. Un importe convertido está en la moneda de **destino**, y así se construye.

**La apertura no es una aportación.** Se modela como un tipo de movimiento propio (`Opening`) para que la suma
del saldo funcione, pero su flujo de caja para el informe **es cero**: no es dinero que se aportara estando la
aplicación mirando, es el punto de partida de una cuenta que ya tenía dinero. Mezclarlos haría que el informe
dijera que se aportó mucho más de lo que se aportó.
## 11. El plan, por trozos

| # | Qué | Depende de | Tamaño |
|---|---|---|---|
| 1 | **Tipos de cambio históricos**, hacia atrás | Decisión 1 = convertir | medio |
| 2 | **`CashLedger`** con su apertura declarada y su migración | — | medio |
| 3 | **Compra y venta generan su fila de efectivo**, con el tipo usado | 1 y 2 | medio |
| 4 | **`CashValue` en la foto** y en la pantalla de la cartera | 2 | pequeño |
| 5 | **El informe con efectivo** y, de verdad, «aportado contra ganado» | 3 y 4 | medio |
| 6 | **La pantalla del saldo**, con la apertura y las correcciones | 2 | medio |
| 7 | Suite de punta a punta y documentación | todas | pequeño |

**El 2 antes que nada**: la tabla y la apertura declarada son lo que hace que el saldo exista, y sin ellas no
hay nada que ajustar. El 1 y el 3 pueden ir juntos.

**Y el 5 es el que cierra el círculo**: con el efectivo, el informe deja de llamarse «coste contra valor» y
pasa a responder la pregunta que siempre se quiso hacer —«puse esto y tengo aquello»— con el dinero que está
en la cuenta incluido.
