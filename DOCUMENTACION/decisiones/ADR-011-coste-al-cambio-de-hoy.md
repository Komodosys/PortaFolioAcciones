# ADR-011 — El coste se convierte al cambio de hoy, no al de la compra

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

## Contexto

El [ADR-003](ADR-003-coste-en-moneda-local.md) decidió guardar el coste en su moneda original **con la
fecha de compra**, para convertir a CAD con el tipo de cambio de ese día. Eso da «lo que ganaste en tu
moneda», pero obliga a conocer la fecha de cada compra.

El 2026-10-12 el usuario planteó cambiarlo y dio su razón, que no es de comodidad sino de modelo mental:

> «Yo compro ese lote en USD porque mi bróker me lo vende en USD, porque las acciones de MSFT son en USD.
> No quiero manejar un tipo de cambio para el momento en que compré el lote porque ya me parece un poco
> complicado.»

Es decir: **para él la operación es en USD, y el CAD es solo la unidad en la que quiere leer el total de
hoy.** Convertir el coste al cambio de la fecha de compra introduce un dato (la fecha) que no forma parte
de cómo él piensa la inversión.

### Los dos métodos, medidos sobre dos posiciones reales

| | Método A (cambio de hoy) | Método B (cambio de compra) |
|---|---|---|
| **NFLX** (compra 20-ene-2026) | coste 2.535,61 → **−523,93 CAD (−20,66 %)** | coste 2.465,48 → **−453,80 CAD (−18,41 %)** |
| **VEEV** (4 lotes de junio-2026) | coste 5.423,74 → **+4.269,34 CAD (+78,72 %)** | coste 5.337,31 → **+4.355,77 CAD (+81,61 %)** |

En el método A, coste y valor se convierten **con el mismo tipo de cambio**, así que el porcentaje de
rendimiento es **idéntico al de la moneda del activo** y el efecto divisa no aparece.

## Decisión

**Método A: el coste y el valor se convierten al tipo de cambio del día de la consulta.**

- Todas las cifras que se muestran —coste, valor, ganancia y porcentaje— usan **el mismo tipo de cambio
  del día**.
- El **porcentaje de rendimiento** que se muestra es, por tanto, **el rendimiento del activo en su propia
  moneda**, expresado en CAD.
- La fecha de compra **deja de ser necesaria** para las cifras principales.
- La conversión con el tipo de cambio de la fecha de compra **se conserva como cálculo opcional** para el
  **desglose del efecto divisa** (previsto para v0.2). Es la única función que necesita fechas.

**Obligación de etiquetado, que forma parte de la decisión:** la aplicación **no puede** llamar a esa
cifra «lo que has ganado en tu moneda». Debe decir lo que es: **rendimiento del activo, expresado en CAD
al cambio de hoy**. La diferencia entre las dos cosas es exactamente el efecto divisa, y presentarla sin
distinguir sería inducir a error.

## Consecuencias

- **Positivas:**
  - **Desaparecen las fechas de compra como requisito** para las cifras principales: los 8 lotes sin
    fecha dejan de ser un problema.
  - **Desaparece toda cifra aproximada.** El coste en CAD es exacto —coste en USD × cambio de hoy—, así
    que las marcas de «coste aproximado» de los ADR-009 y ADR-010 **solo aplican ya al desglose opcional**.
  - El porcentaje de rendimiento es el mismo en las dos monedas, lo que hace el producto **mucho más
    fácil de explicar** a la familia.
- **Negativas / coste asumido, con número:**
  - **El efecto divisa no aparece en la cifra principal.** En NFLX, el método B da −18,41 % y el A
    −20,66 %: esos 2,25 puntos son el colchón que dio la apreciación del dólar. En VEEV, el B da +81,61 %
    y el A +78,72 %. **En un periodo de dólar fuerte, la aplicación mostrará una ganancia en CAD menor
    que la real.** Es una pérdida de información deliberada, aceptada por el usuario.
  - **La línea de «coste» del portafolio en CAD cambia cada día** aunque no se haga ninguna operación,
    porque el coste se reconvierte. Hay que etiquetarla («coste al cambio de hoy») para que no parezca
    un error.
- **Requisito para los snapshots:** cada foto diaria debe guardar las cifras **en la moneda del
  instrumento** además de la conversión usada. Si algún día se cambia de método, el histórico tiene que
  seguir siendo interpretable, y con solo el valor en CAD se perdería.
- **Relación con los ADR anteriores:** refina el [ADR-003](ADR-003-coste-en-moneda-local.md) —guardar el
  coste en su moneda original **sigue siendo obligatorio**, y sigue siendo lo que hace posible el
  desglose del efecto divisa—, pero **la fecha de compra pasa a ser opcional**. Y deja en segundo plano
  las aproximaciones del [ADR-009](ADR-009-carga-inicial-lote-agregado.md) y del
  [ADR-010](ADR-010-se-trabaja-con-lo-que-hay.md).
- **Condición de revisión:** si al ver los totales el usuario echa de menos saber cuánto del resultado
  viene del dólar, el desglose ya está previsto y solo necesita las fechas que se tengan.
