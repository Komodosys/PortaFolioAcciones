# PortaFolioAcciones — Documentación del proyecto

Aplicación web **personal/familiar** (2–5 personas) para el **seguimiento** de portafolios de
inversión: posiciones, precios y rendimiento. Inspirada en la vista de portafolios de Yahoo Finance.

- **Alcance:** solo seguimiento. Sin operaciones/transacciones, sin analítica avanzada.
- **Stack obligatorio:** Angular (frontend), C# / ASP.NET Core (backend), Clean Architecture.
- **Estado:** **diseño cerrado**. Siete ADR aceptados, ocho documentos de análisis y la herramienta de
  verificación de cobertura. **Aún no hay código de la aplicación.** Lo que falta, en
  [TODO.md](TODO.md).

---

## 1. Cómo está organizada esta carpeta

| Documento | Qué contiene |
|---|---|
| [**TODO.md**](TODO.md) | **Lo hecho y lo que falta hasta un producto funcional.** Empieza por aquí |
| [01-inventario-funcionalidades.md](01-inventario-funcionalidades.md) | Inventario funcional de la vista de referencia, con supuestos marcados |
| [02-priorizacion-mvp.md](02-priorizacion-mvp.md) | MVP vs. fase posterior vs. fuera de alcance, con justificación |
| [03-arquitectura-y-capas.md](03-arquitectura-y-capas.md) | Capas, estructura de la solución, regla de dependencias |
| [04-modelo-de-datos.md](04-modelo-de-datos.md) | Modelo de datos a alto nivel, multidivisa, activos de frecuencia distinta |
| [05-precios-y-divisas.md](05-precios-y-divisas.md) | Estrategia de precios y tipos de cambio; comparación de fuentes |
| [06-preguntas-abiertas.md](06-preguntas-abiertas.md) | Decisiones pendientes del usuario, con impacto y recomendación tentativa |
| [07-baseline-tecnologico.md](07-baseline-tecnologico.md) | **Referencia única del stack**: versiones, librerías y decisiones cerradas |
| [08-prueba-cobertura-instrumentos.md](08-prueba-cobertura-instrumentos.md) | Prueba de cobertura de instrumentos reales: plan, hallazgos observados y criterio de decisión |
| [09-guia-de-uso.md](09-guia-de-uso.md) | **Para la familia**: cómo se entra, qué significa cada columna y qué hacer cuando algo no cuadra |
| [10 — Estadísticas para el administrador](10-estadisticas-del-administrador.md) | Análisis de qué informes pedir, qué se puede responder hoy y qué hay que arreglar antes |
| [11 — El efectivo en las carteras](11-efectivo-en-las-carteras.md) | Plan para integrar el registro de efectivo, con la trampa de la divisa y las decisiones que faltan |
| [decisiones/](decisiones/README.md) | Registro de decisiones de arquitectura (ADR) |

## 2. Convenciones

- **Idioma:** documentos y nombres de archivo en español; código y nombres de tipos en inglés
  (convención estándar de .NET y Angular).
- **Nombres:** `NN-tema.md` para análisis; `ADR-NNN-titulo-corto.md` para decisiones.
- **Trazabilidad:** cada decisión relevante nace como ADR en estado `Propuesto` y pasa a
  `Aceptado` cuando el usuario la confirma. El ADR no se reescribe: se sustituye por otro.
- **Supuestos:** todo dato no verificado se marca explícitamente como `[SUPUESTO]`, con la fecha
  en que se asumió.
- **Verificación de datos externos:** precios, planes y límites de proveedores cambian con
  frecuencia. Las cifras de [05-precios-y-divisas.md](05-precios-y-divisas.md) son orientativas y
  deben reconfirmarse contra la página del proveedor antes de contratar.
- **Datos personales anonimizados.** El repositorio se publica, así que los casos reales que se narran
  aquí llevan etiquetas en lugar de nombres y números de cuenta:
  - **Nombres de persona:** `Titular A` y `Titular B`. Son etiquetas, **no** nombres inventados, para que
    nadie los confunda con personas reales.
  - **Números de cuenta:** `RRSP-1`, `RRSP-2`, `RRSP-3`, `TFSA-1`, `TFSA-2`, `TFSA-3` y `TD-1`. Conservan el
    tipo de cuenta, así que las decisiones se siguen entendiendo.
  - **Las cifras son las reales, sin redondear.** Por sí solas no identifican a nadie, y son las que hacen
    que las pruebas demuestren algo.

  Antes de publicar se comprueba con `herramientas\comprobar-datos-personales\ejecutar.cmd`; los detalles
  están en la sección **Privacidad** del [README de la raíz](../README.md).

## 3. Estado de las decisiones

| Decisión | ADR | Estado |
|---|---|---|
| Estructura de solución y regla de dependencias | [ADR-001](decisiones/ADR-001-estructura-y-regla-de-dependencias.md) | **Aceptado** |
| Precios: cierre diario como base, intradía opcional | [ADR-002](decisiones/ADR-002-estrategia-de-precios-cierre-diario.md) | **Aceptado** |
| Coste en moneda local + fecha de compra (no convertir al guardar) | [ADR-003](decisiones/ADR-003-coste-en-moneda-local.md) | **Aceptado** |
| Cobertura de mercados (EE. UU. + Canadá) con presupuesto cero | [ADR-004](decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md) | **Aceptado** (riesgo asumido) |
| PostgreSQL como base de datos desde el inicio | [ADR-005](decisiones/ADR-005-postgresql-desde-el-inicio.md) | **Aceptado** |
| Autenticación local con Identity y cookie | [ADR-006](decisiones/ADR-006-autenticacion-local-identity-cookie.md) | **Aceptado** |
| Minimal APIs con manejadores directos, sin mediador | [ADR-007](decisiones/ADR-007-minimal-apis-sin-mediador.md) | **Aceptado** |
| Lotes por compra desde el MVP | [ADR-008](decisiones/ADR-008-lotes-por-compra.md) | **Aceptado** |
| Carga inicial con un lote agregado y fecha declarada | [ADR-009](decisiones/ADR-009-carga-inicial-lote-agregado.md) | **Aceptado** |
| Se trabaja con los estados disponibles | [ADR-010](decisiones/ADR-010-se-trabaja-con-lo-que-hay.md) | **Aceptado** |
| El coste se convierte al cambio de hoy | [ADR-011](decisiones/ADR-011-coste-al-cambio-de-hoy.md) | **Aceptado** |
| El administrador puede ver los portafolios de los demás | [ADR-012](decisiones/ADR-012-administrador-lectura-portafolios.md) | Sustituido por [ADR-014](decisiones/ADR-014-administrador-escritura-con-auditoria.md) |
| Interfaz multi-idioma (español, francés e inglés) | [ADR-013](decisiones/ADR-013-interfaz-multi-idioma.md) | **Aceptado** |
| El administrador puede modificar datos ajenos, con auditoría | [ADR-014](decisiones/ADR-014-administrador-escritura-con-auditoria.md) | **Aceptado** |

**La arquitectura queda cerrada.** El resumen operativo (versiones y librerías) está en
[07-baseline-tecnologico.md](07-baseline-tecnologico.md).

Respuestas del usuario registradas el **2026-10-12**: ver
[06-preguntas-abiertas.md](06-preguntas-abiertas.md#respuestas-registradas-2026-10-12).

## 4. Orden sugerido de lectura

1. [`TODO.md`](TODO.md) — dónde estamos y qué falta.
2. `06-preguntas-abiertas.md` — qué falta decidir y por qué bloquea.
3. `02-priorizacion-mvp.md` — qué se construye primero.
4. `04-modelo-de-datos.md` y `05-precios-y-divisas.md` — las dos decisiones más costosas de revertir.
5. `03-arquitectura-y-capas.md` — cómo se organiza el código.
6. `08-prueba-cobertura-instrumentos.md` — qué se verificó con datos reales y qué cambió por ello.
