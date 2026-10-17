# 04 — Modelo de datos a alto nivel

Este documento es intencionadamente conceptual: describe entidades, relaciones y las decisiones que
son caras de revertir. No es un esquema de base de datos definitivo.

## Las cinco decisiones que importan

### 1. Un instrumento por cotización, no una jerarquía de tipos

**Opción A — jerarquía de clases** (`Equity : Instrument`, `MutualFund : Instrument`,
`Bond : Instrument`). Modela con precisión, pero obliga a tratar cada tipo de forma distinta en
consultas, en la capa de aplicación y en la API.

**Opción B — una entidad `Instrument` con un tipo y datos opcionales.** Una tabla, un `AssetType`,
y un `ValuationMode` que dice **cómo se obtiene su precio**.

**Recomendación: B.** La diversidad real entre acciones, ETF, fondos y bonos no está en el
comportamiento (todos: cantidad × precio), sino en **cómo se consigue el precio**. Ese es el eje que
conviene modelar explícitamente:

```
ValuationMode = Market     // cotización de mercado (acción, ETF)
              | Nav        // valor liquidativo diario (fondo mutuo)
              | Manual     // lo introduce el usuario (bono individual, fondo privado)
```

Esto convierte "tipos de activos distintos" en un único problema bien resuelto: un `IPriceProvider`
enruta por `ValuationMode` + `PricingFrequency`, y el resto del sistema trata todas las posiciones
igual.

### 2. El coste se guarda en su moneda original, no convertido

| Opción | Qué guarda | Ventajas | Inconvenientes |
|---|---|---|---|
| **A** | `CostPerUnit` + `CostCurrency` + `PurchaseDate`, en la moneda del instrumento | Conserva la verdad original; permite recalcular si cambia la moneda base; permite separar retorno local de efecto divisa | Requiere tipo de cambio histórico siempre |
| **B** | Coste ya convertido a la moneda base | Cálculo inmediato, menos datos | Se pierde el original; si el usuario cambia de moneda base, hay que volver a introducir todo; no se puede mostrar el retorno local |
| **C** | Ambos | Lo mejor de cada uno | Dos verdades que pueden divergir y nadie sabe cuál manda |

**Recomendación: A.** Es la opción correcta y, a esta escala, barata (ver
[ADR-003](decisiones/ADR-003-coste-en-moneda-local.md)). La conversión se calcula al vuelo y, si un
tipo de cambio histórico falta, se degrada de forma explícita en vez de mentir.

### 3. Moneda base: por usuario (con opción de heredarla)

| Opción | Descripción | Valoración |
|---|---|---|
| Global para toda la instalación | Una sola moneda | Simple, pero obliga a que toda la familia vea el resultado en la misma divisa |
| **Por usuario** | Cada persona elige su moneda de visualización | **Recomendado.** 2–5 personas con necesidades distintas; el coste es un campo y un parámetro |
| Por portafolio | Cada portafolio su moneda | Flexible, pero introduce la pregunta "¿y el total de la familia en qué moneda?" sin que nadie la haya pedido |

Implementación: `User.BaseCurrency` como valor por defecto, y `Portfolio.BaseCurrency` **opcional**
(hereda si es nulo). Permite el caso por portafolio sin cargar con él desde el principio.

### 4. Precios: serie temporal + último precio, separados

Guardar **solo el último precio** hace imposible cualquier gráfico histórico. Guardar **solo la
serie histórica** obliga a agregar la tabla completa cada vez que se abre la pantalla.

**Recomendación: las dos cosas.**

- `Prices` — una fila por **(instrumento, fecha)** con el cierre. Es la fuente de verdad y también
  el histórico que alimenta gráficos.
- `LatestQuotes` — una fila por instrumento con el precio más reciente, su fecha/hora, la fuente y
  el retraso. Es la tabla que lee la vista de portafolio. Se puede reconstruir desde `Prices`, así
  que no es un riesgo de consistencia grave.

Un detalle que importa: **cada precio lleva su fecha y su fuente**. La vista nunca debe mostrar un
número sin decir de cuándo es. Con fondos (NAV de ayer) y acciones (cierre de hoy) en la misma
tabla, "de cuándo es" no es un detalle estético.

### 5. Snapshots de portafolio desde el primer día

`PortfolioSnapshots(PortfolioId, Date, BaseCurrency, MarketValue, CostBasis, UnrealizedPnl)`.

Aunque el gráfico histórico sea v0.2, esta tabla debería **escribirse desde v0.1**. Es lo único del
modelo cuyo valor depende del momento en que se empieza: la historia no se puede recuperar hacia
atrás sin transacciones (ver matiz 2 en [01](01-inventario-funcionalidades.md)). Cuesta una fila al
día por portafolio.

---

## Entidades y campos principales

### `Users`
`Id`, `DisplayName`, `Email`, `PasswordHash` (o identidad delegada), `BaseCurrency`,
**`PreferredLocale`**, `IsAdmin`, `IsActive`, `CreatedAtUtc`.

> `IsAdmin` da **lectura y escritura sobre todos los portafolios**, además de la gestión de cuentas y
> contraseñas, y **cada escritura sobre datos ajenos queda auditada**
> ([ADR-014](decisiones/ADR-014-administrador-escritura-con-auditoria.md)). Y `PreferredLocale` (español,
> francés o inglés) es una preferencia **de la persona**, no de la instalación
> ([ADR-013](decisiones/ADR-013-interfaz-multi-idioma.md)).

### `AuditLog`
`Id`, `OccurredAtUtc`, `ActorUserId`, `Action` (crear / modificar / borrar), `EntityType`, `EntityId`,
`OwnerUserId`, `Changes` (**jsonb** con el valor anterior y el nuevo de cada campo afectado), `Reason`
(opcional).

> **Tabla de solo añadir.** La aplicación no permite editarla ni borrarla, y **una regla de PostgreSQL
> rechaza `UPDATE` y `DELETE`**. Y sin clave foránea con cascada hacia la entidad auditada: si se borrara
> una posición, el borrado no puede llevarse por delante la prueba de que existió
> ([ADR-014](decisiones/ADR-014-administrador-escritura-con-auditoria.md)).

### `Portfolios`
`Id`, `OwnerUserId`, `Name`, `BaseCurrency` (nullable → hereda la del usuario), `SortOrder`,
`IsArchived`, `CreatedAtUtc`.

### `Instruments`
`Id`, `Symbol`, `Name`, `AssetType` (Equity | Etf | MutualFund | Bond | BondFund | Index | Cash),
`QuoteCurrency`, `Exchange` (código MIC: XNYS, XETR, XLON…), `Country`, `Isin` (nullable),
`ValuationMode`, `PricingFrequency` (IntradayDelayed | EndOfDay | Daily Nav | Manual),
`ProviderKey` (identificador del instrumento **en la fuente concreta**), `IsActive`.

> `ProviderKey` separado de `Symbol` es importante: el mismo fondo puede llamarse `VFIAX` en una
> fuente y tener otro código en otra. Guardar el símbolo "de pantalla" y el "de proveedor" por
> separado evita tener que traducir en cada llamada.
>
> Y hay un **tercer identificador que no se deduce de los otros: el código del bróker.** En Canadá los
> fondos de Fundserv se identifican por un código de fondo (`TDB2624C`), **no tienen ISIN**, y ese
> código **no existe en la fuente de precios**: buscarlo devuelve cero resultados. La correspondencia se
> resuelve **a mano una vez por instrumento**, confirmando que el NAV del candidato coincide con el del
> estado de cuenta, y se guarda. Es lo que permitirá volver a cargar un extracto del bróker en el
> futuro sin repetir el trabajo. Comprobado el 2026-10-12 con cuatro fondos de TD.

### `Positions`
Es el **agregado**: `Id`, `PortfolioId`, `InstrumentId`, `IsClosed`, **`ClosedAtUtc`**,
**`CostBasisAtClose`**, `Notes`, `CreatedAtUtc`, `UpdatedAtUtc`.

> `CostBasisAtClose` se añadió el 2026-10-12, al implementar la entidad. La política 5 decía que al
> cerrarse una posición «se conserva la fila con `IsClosed = true`» y que esa fila es «el único rastro
> del coste histórico». Pero al venderlo todo **los lotes quedan a cero y su coste se pierde**, así que
> la fila no conservaba nada. Este campo guarda lo que había costado la posición en el momento de
> cerrarla, y es lo que hace cierta esa promesa.

**No guarda cantidad ni coste propios:** eso vive en sus lotes (ver
[ADR-008](decisiones/ADR-008-lotes-por-compra.md)). La cantidad de la posición es la suma de sus
lotes; el coste, la suma del coste convertido de cada uno.

Y para `ValuationMode = Manual`: `ManualPrice`, `ManualPriceCurrency`, `ManualPriceDate`.

### `Lots`
Una fila **por cada compra**: `Id`, `PositionId`, `Quantity`, `CostPerUnit`, `CostCurrency`,
**`PurchaseDate` obligatoria**, **`DateAccuracy`** (`Exacta` | `Aproximada` | `Referencia`), `Notes`,
`CreatedAtUtc`.

En la carga inicial se crea **un único lote agregado** por posición —cantidad total y precio medio, que
es lo que publica el bróker— y se puede partir en lotes reales más adelante sin migrar nada
([ADR-009](decisiones/ADR-009-carga-inicial-lote-agregado.md)).

**Corregido el 2026-10-12 por el [ADR-011](decisiones/ADR-011-coste-al-cambio-de-hoy.md):** la
conversión a moneda base se hace **al tipo de cambio del día de la consulta**, no al de la fecha de cada
lote. Coste y valor se convierten con el **mismo** tipo, así que el tipo se cancela al calcular el
porcentaje y lo que se muestra es **el rendimiento del activo**, expresado en CAD.

La consecuencia está asumida conscientemente: **el efecto divisa no aparece en la cifra principal**. En
un periodo de dólar fuerte, la aplicación mostrará una ganancia en CAD **menor** que la real. Medido con
dos posiciones reales: NFLX −20,66 % (frente a −18,41 % con el cambio de compra) y VEEV +78,72 % (frente
a +81,61 %).

Los lotes se conservan igualmente, porque guardan el coste **en su moneda original**, que es lo que
permitirá reconstruir el efecto divisa más adelante sin volver a introducir datos
([ADR-008](decisiones/ADR-008-lotes-por-compra.md)).

Una **venta parcial no elige lote**: se reparte **proporcionalmente entre todos**, que es lo que hace el
método de coste medio del bróker y lo que conserva el coste por acción. Comprobado con datos reales
(vender 11 de 34 VEEV deja el coste por acción exactamente en el que declara el estado).

> Sobre `CostPerUnit` frente a coste total: guardar el **precio por unidad** es más robusto, porque
> al cambiar la cantidad (un split ajustado a mano, una compra adicional) el coste total se recalcula
> bien. Guardar solo el total deja el precio unitario indefinido cuando cambia la cantidad.

### `Prices`
`InstrumentId`, `PriceDate`, `Close`, `Currency`, `Source`, `RetrievedAtUtc`.
Clave única: `(InstrumentId, PriceDate)`. Si dos fuentes dan el mismo día, manda la fuente
primaria y se registra la discrepancia, no se duplica la fila.

### `LatestQuotes`
`InstrumentId` (clave), `Price`, `Currency`, `AsOfUtc`, `Source`, `DelayMinutes` (nullable),
`MarketState` (nullable), `RetrievedAtUtc`.

### `FxRates`
`FromCurrency`, `ToCurrency`, `RateDate`, `Rate`, `Source`. Clave única:
`(FromCurrency, ToCurrency, RateDate)`.

Se guardan **pares directos**, no solo "todo contra EUR": un proveedor puede dar `USD/CAD` mejor que
la combinación cruzada, y guardar el par tal como llega permite auditar de dónde salió el número.

### `PortfolioSnapshots`
`PortfolioId`, `SnapshotDate`, `BaseCurrency`, `MarketValue`, `CostBasis`, `UnrealizedPnl`.
Clave única: `(PortfolioId, SnapshotDate)`.

---

## Cómo se calcula la ganancia/pérdida consolidada

Por cada posición, en moneda base:

```
Cantidad            = Σ(cantidad de cada lote)
ValorMercado_base   = Cantidad × Precio_actual × FX(moneda_precio → base, HOY)
Coste_base          = Σ(cantidad_lote × coste_unitario_lote) × FX(moneda_coste → base, HOY)
Ganancia            = ValorMercado_base − Coste_base
Ganancia%           = Ganancia / Coste_base
```

> **La misma cifra en las dos monedas, y eso es deliberado.** Al convertir coste y valor con el **mismo**
> tipo de cambio, este se cancela en el porcentaje: el rendimiento mostrado es **el del activo**, tanto
> si se lee en USD como en CAD. Es lo que hace el producto fácil de explicar, y es la decisión del
> [ADR-011](decisiones/ADR-011-coste-al-cambio-de-hoy.md).
>
> Lo que **no** se puede hacer es presentar esa cifra como «lo que has ganado en tu moneda»: eso
> incluiría el efecto divisa, que es precisamente lo que queda fuera. La etiqueta forma parte de la
> decisión, no es cosmética.

Es la definición "el activo medido en su mercado, expresado en la moneda base". **No** mezcla el efecto
del activo con el del tipo de cambio: los mantiene separados a propósito, y el segundo queda pendiente
para el desglose opcional.

**Opciones para más adelante** (no en MVP):

| Enfoque | Qué responde | Coste |
|---|---|---|
| Rendimiento del activo (**el del MVP**) | "¿Cómo se comportó el activo?" | Ya está: es la cifra principal |
| Retorno total en moneda base | "¿Cuánto gané realmente, contando la divisa?" | Bajo si algún día se recuperan las fechas; necesita el FX de cada lote |
| Desglose del efecto divisa | "¿Cuánto gané por el activo y cuánto por la divisa?" | Medio: requiere la descomposición y explicarla bien |

Los tres son correctos; responden a preguntas distintas. En el MVP se enseña el primero porque **es el
único exacto sin depender de fechas que no tenemos** ([ADR-010](decisiones/ADR-010-se-trabaja-con-lo-que-hay.md)).

## Políticas que hay que fijar (y que suelen olvidarse)

1. **Tipo de cambio — revisado el 2026-10-12 por el [ADR-011](decisiones/ADR-011-coste-al-cambio-de-hoy.md).**
   Se usa **el del día de la consulta** para el coste y para el valor, y **la aplicación dice de qué día
   es**. Lo que sigue prohibido es usar un tipo de cambio **en silencio**: toda conversión registra
   **cuál y de qué fecha**, para poder auditar el número. Si el día de la consulta no hay tasa (fin de
   semana, festivo o caída del proveedor), se usa **el último día hábil disponible** y se indica cuál es.
   El tipo de cambio **histórico** queda para v0.2: es lo que necesita el desglose del efecto divisa.

2. **Fecha de compra desconocida — resuelto el 2026-10-12.** Con el
   [ADR-011](decisiones/ADR-011-coste-al-cambio-de-hoy.md), la fecha **deja de afectar a las cifras
   principales**, así que el problema desaparece del MVP. La fecha sigue siendo **obligatoria en el
   modelo** y lleva una marca de exactitud (`Exacta` | `Aproximada` | `Referencia`, ver `Lots`), porque
   es lo único que permitirá calcular el efecto divisa cuando se quiera. Pero **no bloquea nada**: una
   fecha marcada como `Referencia` no estropea ninguna de las cifras que se muestran hoy.

3. **Precisión y redondeo.** Nunca `double` para dinero. `decimal` en C# y `DECIMAL`/`NUMERIC` en la
   base. Precisión interna: precio 6 decimales, cantidad 8 (los fondos usan fracciones),
   tipo de cambio 8, importe 4. **Redondear solo al presentar**, con la convención de la moneda
   (2 decimales habitual; 0 en JPY; 4 en algunos importes de FX). Si se redondea en cada paso
   intermedio, los totales dejan de cuadrar con la suma de las filas y el usuario lo nota.

4. **Moneda del redondeo final.** El total del portafolio se redondea en moneda base, una sola vez,
   sobre la suma exacta. No se suman filas ya redondeadas.

5. **Cantidad cero y posiciones cerradas.** Se conserva la fila con `IsClosed = true`; nunca se
   borra automáticamente. Sin transacciones, esa fila es el único rastro del coste histórico.

6. **Instrumentos duplicados.** El mismo ETF comprado en dos mercados o en dos monedas son **dos
   instrumentos distintos** (`Exchange` + `QuoteCurrency` forman parte de su identidad). No
   fusionarlos: la identidad "solo por símbolo" provoca colisiones reales entre bolsas.

7. **Aislamiento entre usuarios.** `OwnerUserId` en `Portfolios`; las posiciones no son accesibles
   más que a través de su portafolio. Dos barreras: un filtro global de consulta en EF Core por
   usuario actual **y** una comprobación explícita en cada caso de uso. La primera evita olvidos; la
   segunda evita que un filtro mal configurado se convierta en una fuga. Un test de integración debe
   verificar que el usuario B recibe 404 (no 403, que ya revela que el recurso existe) al pedir el
   portafolio del usuario A.

8. **Una compra o varias: lotes.** El bróker publica el **coste medio**, pero una posición puede venir
   de compras en fechas distintas. **Decidido el 2026-10-12: se modelan lotes desde el MVP**
   ([ADR-008](decisiones/ADR-008-lotes-por-compra.md)), porque guardan el coste **en su moneda
   original** y su fecha, que es lo único que permitirá reconstruir el efecto divisa.

   **Una venta parcial no elige lote:** la reducción se reparte **proporcionalmente** entre todos, que es
   lo que hace el método de coste medio del bróker y lo que conserva el coste por acción. Verificado con
   datos reales: vender 11 de 34 VEEV deja el coste por acción exactamente en el que declara el estado.

   La carga inicial es **una sola fila por posición**
   ([ADR-009](decisiones/ADR-009-carga-inicial-lote-agregado.md)), así que introducir datos no es
   trabajoso. Y **nunca** se usa un tipo de cambio en silencio.
