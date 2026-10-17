# ADR-008 — Lotes por compra desde el MVP

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

## Contexto

El [ADR-003](ADR-003-coste-en-moneda-local.md) decidió guardar el coste en la moneda original **junto
con la fecha de compra**, porque la moneda base es el CAD y las posiciones están en USD: el coste en
moneda base es `coste × FX(fecha de compra)`, y sin fecha no hay tipo de cambio que aplicar.

Ese ADR presuponía **una** fecha de compra por posición. El 2026-10-12, al verificar una posición real
de principio a fin, la suposición quedó falsificada:

- **NFLX** (cuenta RRSP-2): una sola compra, el 2026-01-20, 20 acciones a 89,00 USD. La conversión es
  exacta: 1.780,00 USD × 1,3851 = 2.465,48 CAD.
- **MSFT** (misma cuenta): 12 acciones con coste medio 478,09. En realidad son **dos compras**: 8
  anteriores a enero de 2026 (fecha desconocida) y 4 del 2026-01-29 a 425,00. El bróker solo publica el
  **coste medio**, no las compras.

Y la fecha de compra **solo es recuperable parcialmente**: la sección de actividad de los estados de
cuenta la trae, pero únicamente para las operaciones **dentro del periodo del estado**. Las compras
anteriores no aparecen.

## Opciones consideradas

1. **Coste medio con una fecha de referencia.** Una fila por posición. Simple de introducir y de
   mantener. El coste en CAD se calcula con **una** fecha, que para una posición de varias compras es
   una aproximación: el error de conversión es real, silencioso y no acotado.
2. **Lotes desde el MVP.** Una fila por compra, con su cantidad, su coste unitario y su fecha. La
   conversión a CAD es exacta lote a lote. A cambio, hay que introducir y mantener el historial de
   compras.
3. **Coste medio ahora, lotes después.** Construir solo el promedio y añadir lotes más adelante. Se
   descarta porque obligaría a migrar datos y a recalcular costes ya mostrados al usuario.

## Decisión

**Opción 2: lotes desde el MVP.**

El motivo es que el objetivo declarado del ADR-003 es un coste en CAD **correcto**, y con la opción 1
la conversión de una posición multimoneda comprada en varias fechas es una aproximación que nadie ve.
El usuario ha elegido precisión sobre comodidad de introducción, de forma consciente y conociendo el
coste en trabajo manual.

Consecuencias de modelo:

- `Position` deja de tener cantidad y coste propios: pasa a ser el **agregado** (portafolio +
  instrumento) y su composición son **N `Lots`**.
- Cada `Lot` lleva: `Quantity`, `CostPerUnit`, `CostCurrency`, **`PurchaseDate` obligatoria** y `Notes`.
- La valoración de la posición es la **suma de sus lotes**:
  - cantidad total = Σ `Quantity` (la del lote, ajustada por splits);
  - valor de mercado = cantidad total × precio (independiente de los lotes);
  - coste en moneda base = **Σ (`Quantity` × `CostPerUnit` × FX(`PurchaseDate`))** — la conversión es
    **por lote**, no por posición.
- Esto hace además que el **desglose del efecto divisa** (previsto para v0.2) sea exacto en lugar de
  aproximado.

## Consecuencias

- **Positivas:** coste en moneda base exacto; fechas reales por compra; el desglose del efecto divisa
  sale bien; y el bróker publica el coste por unidad de cada operación, así que los datos existen.
- **Negativas / coste asumido, que es real y hay que decirlo:**
  - **Más trabajo de introducción.** Cada compra es una fila. La sección de actividad de los estados
    cubre las operaciones recientes, no el historial completo: las compras antiguas habrá que
    teclearlas una vez.
  - **Las ventas: resuelto el 2026-10-12 con datos reales.** Una venta **no elige un lote**: se
    reparte **proporcionalmente entre todos**, que es lo que hace el método de coste medio que usa el
    bróker (y el que exige Hacienda en Canadá para propiedades idénticas). Verificado con la cartera
    real: al vender 11 de 34 VEEV, los cuatro lotes se escalan por 23/34 y el coste por acción que
    queda (165,5422) es **exactamente** el que declara el estado. Esto elimina la pregunta de «qué lote
    se reduce» y garantiza que la aplicación coincida con el bróker.
  - **Un dato que conviene saber:** a efectos fiscales canadienses, el coste de propiedades idénticas se
    calcula por **promedio** (adjusted cost base), no por lote. Es decir, los lotes aquí son para que
    **el rendimiento y la conversión de moneda sean correctos**, no para la declaración de impuestos.
- **Requisito derivado para la carga inicial:** hay que **extender el extractor para leer la sección de
  actividad** de los estados de cuenta y proponer los lotes que aparezcan allí. Es la fuente natural de
  este dato y evita teclear a mano lo que ya está en un PDF.
- **Condición de revisión:** si la introducción de lotes se convierte en la razón por la que la
  aplicación no se usa, habrá que revisar esta decisión y ofrecer el modo de coste medio.
