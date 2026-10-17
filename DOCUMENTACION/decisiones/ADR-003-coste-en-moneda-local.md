# ADR-003 — Guardar el coste en su moneda original con fecha de compra

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

> **Base de la aceptación (2026-10-12):** el usuario confirmó que la **moneda base es el dólar
> canadiense (CAD)**, mientras que parte de la cartera está denominada en **USD** y potencialmente en
> otras divisas. La conversión es por tanto inevitable para las posiciones no canadienses, y con el
> tipo de cambio en juego, guardar el importe ya convertido destruiría el dato original sin ganar
> nada. El par principal a cubrir es **USD/CAD**, el más líquido y mejor documentado del mundo: no
> presenta ninguna dificultad de obtención.

## Contexto

Las posiciones pueden estar denominadas en varias monedas y las métricas de rendimiento se consolidan
en una moneda base por usuario. Hay que decidir **qué se guarda** cuando el usuario introduce una
compra: el importe tal como lo pagó, o ese importe ya convertido.

El sistema **no registra transacciones**, así que el coste introducido a mano es el único dato de
origen disponible. Si se guarda mal, no hay forma de reconstruirlo después.

Restricciones que aplican: tipos de cambio históricos disponibles en fuentes gratuitas (tipos del
BCE, con histórico completo); la moneda base puede cambiar de opinión con el tiempo; y el usuario ha
pedido explorar cómo la conversión afecta a las ganancias y pérdidas consolidadas.

## Opciones consideradas

1. **Guardar el coste ya convertido a la moneda base.**
   - Ventajas: el cálculo de G/P es inmediato, sin dependencia de tipos históricos.
   - Inconvenientes: se pierde el importe original; si el usuario cambia de moneda base hay que
     reintroducir todas las posiciones; no se puede mostrar el retorno en la moneda del activo ni
     separar el efecto divisa; y si la conversión se hizo con un tipo equivocado, el error es
     irreversible.
2. **Guardar el coste en la moneda del instrumento, junto con la fecha de compra.**
   - Ventajas: conserva el dato original; permite recalcular con cualquier moneda base; habilita
     retorno local y desglose del efecto divisa; y el tipo de cambio aplicado queda trazable.
   - Inconvenientes: exige un tipo de cambio histórico por posición; hay que definir qué hacer
     cuando falta.
3. **Guardar ambos** (original y convertido).
   - Ventajas: ninguno que no dé ya la opción 2.
   - Inconvenientes: dos verdades que pueden divergir sin que nadie sepa cuál manda. Es una fuente
     garantizada de incoherencias contables.

## Decisión

**Opción 2.** `Positions` guarda `CostPerUnit`, `CostCurrency` y `PurchaseDate` en la moneda del
instrumento. La conversión a la moneda base es siempre un cálculo derivado, nunca un dato
almacenado, y registra qué par y qué fecha se aplicaron.

Política para el tipo de cambio faltante: se busca la fecha de compra exacta; si no existe (fin de
semana, festivo o moneda no publicada por el BCE), se usa el día hábil anterior más cercano dentro
de una ventana corta; si tampoco hay, la posición se marca como *pendiente de tipo de cambio* y
**queda excluida del consolidado**, con un aviso visible. Queda **prohibido** usar en silencio el
tipo de cambio actual o un 1:1.

Se guarda el coste **por unidad**, no el total: al cambiar la cantidad (un split ajustado a mano),
el coste total se recalcula correctamente en vez de quedar indefinido.

**Matiz detectado con datos reales (2026-10-12), que la decisión no preveía.** Se presuponía *una*
fecha de compra por posición, y eso no siempre se cumple:

- Hay posiciones de **una sola compra**, y entonces la conversión es exacta. Ejemplo real: NFLX, 20
  acciones compradas el 2026-01-20 a 89,00 USD → 1.780,00 USD → 2.465,48 CAD con el USD/CAD de ese día
  (1,3851).
- Hay posiciones de **varias compras en fechas distintas**, y el bróker solo publica el **coste medio**.
  Es el caso de MSFT en la cuenta RRSP-2: 12 acciones con coste medio 478,09 = 8 compradas antes de
  enero de 2026 (fecha desconocida) + 4 compradas el 2026-01-29 a 425,00.

Y la fecha de compra **no siempre es recuperable**: la sección de actividad de los estados de cuenta sí
la trae, pero solo para las operaciones **dentro del periodo del estado**. Las compras anteriores no
aparecen.

Consecuencia: había que decidir entre modelar **lotes** (una fila por compra, conversión exacta, pero
exige un historial que los estados no dan completo) o mantener **coste medio con una fecha de
referencia**. **Resuelto el mismo día: se modelan lotes desde el MVP**
([ADR-008](ADR-008-lotes-por-compra.md)), de modo que la conversión se hace **lote a lote** con el tipo
de cambio de cada fecha.

**Refinado por el [ADR-011](ADR-011-coste-al-cambio-de-hoy.md) (2026-10-12).** Guardar el coste en su
moneda original **sigue siendo obligatorio** —es lo que hace posible el desglose del efecto divisa—,
pero **la fecha de compra pasa a ser opcional**: las cifras que se muestran convierten el coste **al tipo
de cambio del día**. Conviene notar la aparente contradicción: este ADR prohibía usar «en silencio el tipo
de cambio actual»; el ADR-011 lo autoriza **etiquetándolo**. Lo prohibido era el silencio, no el tipo.

## Consecuencias

- **Positivas:** el dato original nunca se pierde; cambiar de moneda base no exige reintroducir
  nada; es posible mostrar retorno local y retorno total, y más adelante separar el efecto divisa;
  cualquier número se puede auditar hasta el par y la fecha que lo produjeron.
- **Negativas / coste asumido:** la **fecha de compra pasa a ser un dato necesario** para que la
  posición entre en el consolidado. Si el usuario no la recuerda, la posición queda parcialmente
  valorada. Es el precio de no producir números falsos, y se acepta.
- **Deuda asumida:** la ventana de búsqueda del tipo de cambio y la lista de divisas no cubiertas
  por el BCE quedan pendientes de concretar cuando se conozca la lista real de monedas (pregunta C1
  en [06-preguntas-abiertas.md](../06-preguntas-abiertas.md)).
- **Condición de revisión:** si en algún momento se decide registrar transacciones, esta decisión
  sigue siendo válida y además pasa a ser la base natural del cálculo de coste medio. No hay que
  revertirla.
