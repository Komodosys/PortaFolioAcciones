# 01 — Inventario de funcionalidades de seguimiento

## Nota sobre la fuente

La URL de referencia (`ca.finance.yahoo.com/portfolio/<id>/view/v1`) **no es accesible sin sesión**:
el portafolio es privado del usuario, así que no pude abrirla ni verificarla. Este inventario se
construye a partir de mi conocimiento de la vista de portafolios de Yahoo Finance y de
aplicaciones equivalentes (Google Finance, Sharesight, Portfolio Performance, KMyMoney,
Snowball Analytics).

**Marcas usadas:**

- `[C]` = funcionalidad que doy por conocida y estable en Yahoo Finance.
- `[S]` = **supuesta**: el nombre exacto de la columna, el detalle del cálculo o su presencia
  actual pueden variar. Hay que verificarlas con una sesión real antes de cerrar el MVP.
- `[A]` = funcionalidad adyacente de aplicaciones similares, no necesariamente en Yahoo.

Supuestos asumidos a la fecha de este documento. Si al abrir la vista real algo no coincide, se
corrige este archivo y se anota en el registro de decisiones.

---

## 1. Gestión de portafolios

| # | Funcionalidad | Marca | Nota |
|---|---|---|---|
| 1.1 | Múltiples portafolios por usuario | C | Yahoo permite varias listas/portafolios |
| 1.2 | Crear, renombrar y eliminar portafolios | C | |
| 1.3 | Duplicar portafolio | S | |
| 1.4 | Portafolio por defecto / último abierto | A | |
| 1.5 | Portafolio de seguimiento (watchlist) separado de posiciones reales | C | En nuestro alcance: **fuera** (no son posiciones) |

## 2. Gestión de posiciones

| # | Funcionalidad | Marca | Nota |
|---|---|---|---|
| 2.1 | Añadir posición por símbolo (buscador de instrumentos) | C | Requiere catálogo de símbolos |
| 2.2 | Cantidad / número de títulos | C | |
| 2.3 | Coste: precio medio de compra *o* coste total | S | Yahoo pide coste por acción |
| 2.4 | Fecha de compra | S | Clave para el FX histórico |
| 2.5 | Notas por posición | C | |
| 2.6 | Editar / eliminar posición | C | |
| 2.7 | Importar posiciones desde CSV / plantilla | S | Yahoo lo tuvo vía plantilla |
| 2.8 | Ajuste manual de cantidad (splits, traspasos) | A | Ver *matices de alcance* (abajo) |
| 2.9 | Precio manual para activos sin cotización pública | A | Bonos, fondos privados, planes de pensión |
| 2.10 | Posición marcada como cerrada (histórico) | A | Alternativa: borrar |

## 3. Listado de posiciones (columnas)

Columnas que Yahoo Finance muestra o permite mostrar `[C]`/`[S]`. Las de `[S]` llevan el matiz de
nombre exacto.

**Identificación**

- Símbolo / ticker `[C]`
- Nombre del instrumento `[C]`
- Tipo de activo `[S]`
- Moneda de cotización `[C]`
- Bolsa / mercado `[S]`

**Precio y mercado**

- Último precio `[C]`
- Cambio del día (absoluto y %) `[C]`
- Rango del día `[C]`
- Rango de 52 semanas `[C]`
- Volumen y volumen medio `[S]`
- Capitalización `[C]`
- P/E, EPS `[S]`
- Hora/fecha del último precio y aviso de retraso `[S]`

**Posición y rendimiento**

- Cantidad `[C]`
- Coste por unidad / coste total (book value) `[C]`
- Valor de mercado `[C]`
- Ganancia/pérdida absoluta `[C]`
- Ganancia/pérdida % `[C]`
- Ganancia/pérdida del día (absoluta y %) `[C]`
- Peso de la posición en el portafolio (weight %) `[C]`
- Valor de mercado en moneda base `[S]` (Yahoo muestra la moneda del portafolio)

**Metadatos del usuario**

- Notas `[C]`
- Fecha de compra `[S]`

## 4. Métricas agregadas del portafolio

| # | Métrica | Marca |
|---|---|---|
| 4.1 | Valor total de mercado | C |
| 4.2 | Coste total | C |
| 4.3 | Ganancia/pérdida total (absoluta y %) | C |
| 4.4 | Ganancia/pérdida del día (absoluta y %) | C |
| 4.5 | Posición en efectivo / liquidez | S |
| 4.6 | Nº de posiciones | A |
| 4.7 | Mejor/peor posición del día o del periodo | A |

## 5. Vistas, orden y filtros

| # | Funcionalidad | Marca |
|---|---|---|
| 5.1 | Ordenar por cualquier columna | C |
| 5.2 | Elegir columnas visibles y guardar la vista | S |
| 5.3 | Filtrar por tipo de activo / mercado / moneda | A |
| 5.4 | Agrupar posiciones (por tipo, sector, moneda) | A |

## 6. Gráficos

| # | Funcionalidad | Marca | Nota |
|---|---|---|---|
| 6.1 | Evolución del valor del portafolio (1D, 5D, 1M, 6M, YTD, 1A, 5A, Máx.) | S | Requiere histórico de valoraciones, no solo de precios |
| 6.2 | Rendimiento comparado con un índice (p. ej. S&P 500) | S | Aparece en la sección de análisis de Yahoo |
| 6.3 | Asignación por sector / país / tipo de activo | S | |
| 6.4 | Principales posiciones (top holdings) | S | |
| 6.5 | Mini-gráfico por fila (sparkline) | S | |

## 7. Multidivisa

| # | Funcionalidad | Marca | Nota |
|---|---|---|---|
| 7.1 | Precio en la moneda nativa del instrumento | C | |
| 7.2 | Conversión del portafolio a una moneda de visualización | C | Yahoo usa la moneda del usuario |
| 7.3 | Cambio de la moneda de visualización | S | |
| 7.4 | Tipos de cambio históricos para el coste | A | Yahoo no lo desglosa; otras apps sí |

## 8. Rendimiento y análisis

| # | Funcionalidad | Marca | Nota |
|---|---|---|---|
| 8.1 | Rendimiento del periodo (simple, sobre coste) | C | |
| 8.2 | Rentabilidad ponderada por dinero (MWR/IRR) o por tiempo (TWR) | S | Requiere flujos de caja |
| 8.3 | Beta, volatilidad, Sharpe | S | Analítica avanzada |
| 8.4 | Dividendos cobrados / rentas | C | Fuera de alcance declarado |
| 8.5 | Desglose del efecto divisa vs. efecto del activo | A | No está en Yahoo de forma explícita |

## 9. Datos y frescura

| # | Funcionalidad | Marca |
|---|---|---|
| 9.1 | Indicador de precio retrasado (p. ej. 15 min) | C |
| 9.2 | Indicador de mercado abierto/cerrado por bolsa | S |
| 9.3 | "As of" (fecha/hora del dato) por posición | S |
| 9.4 | Refresco manual del portafolio | C |
| 9.5 | Refresco automático periódico | S |

## 10. Exportación y avisos

| # | Funcionalidad | Marca |
|---|---|---|
| 10.1 | Exportar a CSV / imprimir | S |
| 10.2 | Alertas de precio por posición | C |
| 10.3 | Resumen por correo | A |

---

## Matices de alcance detectados

Estos son los puntos donde una funcionalidad **aparentemente básica de seguimiento** choca con la
restricción "sin operaciones ni transacciones". No son opiniones: son consecuencias técnicas.

1. **Acciones corporativas (splits y dividendos en acciones).** Al no registrar transacciones, nada
   ajusta automáticamente la cantidad ni el coste. Si una acción hace un split 2:1 y la cantidad
   sigue siendo la antigua, el valor de mercado se duplica y la ganancia aparece inflada.
   *Opción MVP:* permitir editar cantidad y coste a mano, y anotarlo en las notas de la posición.
   *Opción posterior:* manejar splits automáticamente desde la fuente de datos.
   **Comprobado el 2026-10-12:** la fuente gratuita ya entrega los eventos de splits
   (`events=div,splits`), así que la opción posterior **no requiere un proveedor de pago**. Y el riesgo
   es real, no teórico: Netflix —posición de la cartera— tuvo un split 10:1 el 2025-11-17. Ver
   [08](08-prueba-cobertura-instrumentos.md).

2. **Gráfico histórico de rendimiento (6.1).** Sin flujos de caja no se puede reconstruir el valor
   pasado del portafolio: solo se conocen las posiciones de *hoy* y los precios de *ayer*. Valorar
   la cartera actual hacia atrás es una aproximación, y es incorrecta si hubo altas o bajas en el
   periodo. *Opciones:* (a) guardar una foto diaria del valor desde que se usa la app y construir
   el gráfico hacia delante — recomendado; (b) repreciar las posiciones actuales hacia atrás,
   documentándolo como aproximación; (c) introducir transacciones, que está fuera de alcance.

3. **Bonos individuales.** Un bono no se valora como una acción: se cotiza en porcentaje del
   nominal, el precio relevante es el "sucio" (que incluye el cupón corrido), y para valorarlo bien
   hacen falta el nominal, el cupón y las fechas de pago. Modelar el cupón corrido es entrar en
   territorio de transacciones/flujos. *Opción:* en el MVP tratar la renta fija como fondos y ETF
   de bonos (se valoran como cualquier instrumento) y dejar los bonos individuales con **valoración
   manual**.

4. **Fondos mutuos: un solo precio al día.** El NAV se publica una vez al día, después del cierre,
   y a veces con un día de retraso. Durante la sesión, la posición simplemente no tiene precio
   nuevo. Esto no es un fallo: hay que mostrarlo con su fecha y no mezclarlo silenciosamente con
   precios intradía. (Ver [05-precios-y-divisas.md](05-precios-y-divisas.md).)

5. **Efectivo / liquidez (4.5).** Yahoo lo muestra. En un modelo sin transacciones, el efectivo
   solo puede ser un dato introducido a mano. Se puede incluir como "posición especial" o dejar
   fuera del MVP.

6. **Precio manual (2.9).** Para bonos individuales, fondos privados o planes de pensión donde la
   fuente pública no existe o no es fiable. Aunque parezca un parche, es lo que hace que la app
   sirva para una cartera real; conviene diseñarlo desde el modelo de datos y no como excepción.

7. **Fecha de compra (2.4).** Parece un detalle, pero con multimoneda es **obligatoria**: sin fecha
   de compra no hay tipo de cambio histórico y la ganancia/pérdida en moneda base no es calculable.
   Si el usuario no la recuerda, hace falta una política explícita (ver
   [04-modelo-de-datos.md](04-modelo-de-datos.md)).

8. **Venta de posiciones (2.10).** Sin transacciones, "vender" solo puede significar borrar la
   posición o poner cantidad a cero. Si se borra, se pierde la referencia del coste y no se puede
   reconstruir nada. Recomendación: permitir cerrar posiciones con cantidad 0 y conservarlas o
   archivarlas, en vez de borrar.
