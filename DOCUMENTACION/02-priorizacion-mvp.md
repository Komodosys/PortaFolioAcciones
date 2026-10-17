# 02 — Priorización: MVP y fase posterior

## Criterios usados para priorizar

Para un proyecto personal/familiar, el orden no lo decide la vistosidad sino cuatro filtros:

1. **Responde a la pregunta principal:** "¿cuánto tengo y cuánto he ganado, en mi moneda?" Todo lo
   que no contribuya a eso, sale del MVP.
2. **Datos que se puedan introducir a mano en dos minutos por posición.** Si una funcionalidad
   exige alimentar la app con información que el usuario no tiene a mano, se abandona en la
   práctica.
3. **Sin dependencia de pago obligatoria.** El MVP debe funcionar con fuentes gratuitas; el gasto
   en datos se decide cuando se conozca la lista real de mercados.
4. **Sin transacciones.** Todo lo que implique reconstruir flujos de caja (rentabilidades
   ponderadas, cupones, dividendos, plusvalías fiscales) queda fuera por definición de alcance.

Un quinto criterio, específico de este proyecto: **la diversidad de tipos de activos manda sobre la
profundidad.** Para 3 personas con acciones, ETF, algún fondo y algún bono, vale más que los cuatro
tipos se muestren de forma correcta y honesta (con su fecha de precio) que tener análisis de sector
perfecto solo para acciones americanas.

---

## MVP (v0.1) — "mi cartera, hoy, en mi moneda"

### Cuentas y aislamiento

| Funcionalidad | Por qué entra |
|---|---|
| Login local, cuentas creadas por el administrador (sin registro público) | Son 2–5 personas conocidas; el registro abierto solo añade riesgo |
| Un usuario → varios portafolios | Cada persona puede separar, p. ej., "largo plazo" y "emergencia" |
| Portafolios estrictamente privados por defecto | Requisito explícito |
| Moneda base configurable por usuario | Cada persona puede ver su resultado en la moneda que le es útil |

### Posiciones y datos de entrada

| Funcionalidad | Por qué entra |
|---|---|
| Alta/edición/borrado de posición con: instrumento, cantidad, coste, moneda del coste, fecha de compra, notas | Es el corazón del producto |
| Catálogo de instrumentos con **búsqueda por nombre**, guardado de **la equivalencia con el bróker** y alta manual | Sin catálogo no hay precios. Comprobado con la cartera real: los fondos mutuos canadienses **no tienen ISIN** (son de Fundserv), su código `TDB2624C` **no se indexa** en la fuente, y esta los identifica con un código interno opaco. Cada fondo exige resolver **una vez** la equivalencia «código del bróker → símbolo de la fuente» y guardar los dos. Ver [08](08-prueba-cobertura-instrumentos.md) |
| **Verificación de identidad de un instrumento contra el estado de cuenta** (comparar su precio con el del extracto) | Es el único modo de distinguir series de un mismo fondo con nombres casi idénticos y NAV distinto: en la cartera real, «TD Canadian Equity Pool Series W» y «…Class Series W» difieren en más de 5 dólares de NAV. Un fondo mal emparejado produce cifras falsas sin que nada falle |
| Cerrar posición (cantidad 0) sin borrarla | Evita perder el coste histórico; más barato ahora que migrar datos después |
| Precio manual por posición (con fecha y moneda) | Único modo de valorar fondos sin fuente pública y activos ilíquidos. Al haber dejado fuera los bonos individuales cubre menos casos de los previstos, pero sigue siendo la válvula de escape que hace viable el alcance |
| Fecha de compra obligatoria, con opción "no la recuerdo" que marca la posición | Sin fecha no hay FX histórico; ver [04](04-modelo-de-datos.md) |

### Precios y divisas

| Funcionalidad | Por qué entra |
|---|---|
| Precios de **cierre diario** para acciones, ETF y fondos soportados por la fuente | Es el mínimo común denominador entre tipos de activo y el más barato |
| Tipos de cambio diarios de cierre y su histórico | Sin esto no hay métricas consolidadas en moneda base |
| Job diario que actualiza precios y FX tras el cierre de cada mercado | Con cierre diario basta una ejecución al día |
| Refresco manual bajo demanda | Da sensación de control sin coste de API |
| "As of" visible por posición y marca de dato obsoleto | Honestidad del dato: es la diferencia entre una app fiable y una que engaña |

### Vista de portafolio

| Funcionalidad | Por qué entra |
|---|---|
| Tabla de posiciones con las columnas esenciales: símbolo, nombre, moneda, cantidad, coste, precio, valor de mercado, G/P absoluta, G/P %, peso, fecha del precio | Cubre el 90 % del uso diario |
| Totales del portafolio: valor, coste, G/P total, G/P del día | La pregunta principal |
| Ordenación por columna | Coste de implementación mínimo, uso constante |
| Vista responsive (uso desde móvil) | Es una app familiar; el móvil será el dispositivo habitual |
| Mostrar total parcial con aviso si falta algún precio | Nunca inventar un total silenciosamente |

### Calidad

| Funcionalidad | Por qué entra |
|---|---|
| Tests unitarios del dominio: valoración, conversión, redondeo | Es donde un error se propaga a todos los números |
| Tests de aislamiento entre usuarios | Es el requisito de seguridad real del proyecto |

---

## Fase posterior (v0.2 en adelante)

| Funcionalidad | Fase | Por qué se pospone |
|---|---|---|
| **Snapshots diarios de valor del portafolio** | v0.2 (pero conviene **empezar a guardarlos ya en v0.1**) | Habilita el gráfico histórico. Empezar a guardarlos en v0.1 cuesta casi nada y evita perder meses de historia |
| Gráfico de evolución del portafolio | v0.2 | Depende de los snapshots; sin ellos es una aproximación engañosa |
| Precios intradía con 15 min de retraso (opt-in, durante sesión) | v0.2 | Multiplica el consumo de API y el coste, y no mejora la decisión de un inversor de largo plazo |
| Desglose del efecto divisa (retorno local vs. retorno por FX) | v0.2 | Muy valioso para carteras multimoneda, pero es una capa de cálculo encima de lo básico |
| Importación CSV desde el broker | v0.2 | Ahorra teclear 30 posiciones, pero depende del formato de cada broker; se hace cuando duela |
| Asignación por tipo de activo / sector / país (gráficos) | v0.2 | Requiere enriquecer el catálogo de instrumentos con datos que no todas las fuentes gratuitas dan |
| Comparación con un índice de referencia | v0.2 | Es técnicamente "un instrumento más"; se pospone por valor, no por dificultad |
| Alertas de precio (email) | v0.2 | Requiere infraestructura de envío de correo |
| Exportación a CSV / PDF | v0.2 | Útil para respaldo, no para el uso diario |
| Segundo factor de autenticación (TOTP) | v0.2 | Recomendable, pero es más importante que la app no sea pública |
| Detección de splits (avisar al usuario) | v0.2 | Se había pospuesto a v0.3 por suponer que hacía falta un proveedor de eventos corporativos. **Comprobado: la fuente gratuita ya los entrega** (`events=div,splits`). En el MVP se ajusta a mano, pero la aplicación debe avisar — Netflix tuvo un split 10:1 el 2025-11-17 y es una posición real de la cartera |
| Agrupación/filtros por moneda o mercado | v0.2 | Poco coste, poco uso al principio |

---

## Fuera de alcance (explícito, por ahora)

- Registro de operaciones de compra/venta, plusvalías fiscales, informes para Hacienda.
- Dividendos y cupones cobrados, efectivo, flujos de caja.
- Analítica avanzada: Sharpe, beta, volatilidad, atribución, optimización.
- Compartir portafolios entre usuarios, roles, permisos granulares, organizaciones.
- Multi-tenant de verdad (varias familias/empresas), límites de uso, planes de suscripción.
- Aplicación móvil nativa (basta con la web responsive).
- Suscripciones a datos en tiempo real con licencia profesional.

---

## Notas de alcance que el usuario debería confirmar

1. **Bonos individuales** dentro o fuera del MVP. Si entran, entran con valoración manual, no con
   cotización automática. Es la decisión que más cambia el modelo de datos.
2. **Efectivo/liquidez** dentro o fuera. Si entra, entra como posición manual.
3. **Snapshots desde el primer día**: recomiendo sí, aunque el gráfico llegue después. Es la única
   funcionalidad de esta lista cuyo valor depende de *cuándo se empieza*, porque la historia no se
   puede recuperar hacia atrás.
