# Registro de decisiones de arquitectura (ADR)

Un **ADR** (Architecture Decision Record) es un documento corto que registra **una** decisión, el
contexto en que se tomó y sus consecuencias. Sirve para que dentro de seis meses nadie tenga que
preguntarse "¿por qué está esto así?".

## Reglas de esta carpeta

1. **Una decisión por archivo.** Si un ADR necesita explicar dos decisiones, son dos ADR.
2. **Un ADR aceptado no se reescribe.** Si la decisión cambia, se crea un ADR nuevo que **sustituye**
   al anterior, y el anterior pasa a estado `Sustituido por ADR-NNN`.
3. **Estados:** `Propuesto` → `Aceptado` / `Rechazado` → `Sustituido por ADR-NNN`.
4. **Nombres:** `ADR-NNN-titulo-corto-en-minusculas.md`, numeración correlativa sin huecos.
5. **Cuándo crear uno:** cuando la decisión es cara de revertir, cuando hay más de una opción
   razonable, o cuando alguien podría preguntarse en el futuro por qué se hizo así. No para
   decisiones triviales o fácilmente reversibles.
6. **Idioma:** español. Código y nombres de tipos, en inglés.
7. **Índice:** cada ADR nuevo se añade a la tabla de la sección *Estado de las decisiones* en
   [../README.md](../README.md).

## Plantilla

```markdown
# ADR-NNN — Título de la decisión

- **Estado:** Propuesto | Aceptado | Rechazado | Sustituido por ADR-NNN
- **Fecha:** AAAA-MM-DD
- **Decide:** (quién)

## Contexto

Qué situación obliga a decidir. Restricciones que aplican. Qué se sabe y qué no.

## Opciones consideradas

1. **Opción A** — descripción. Ventajas / inconvenientes.
2. **Opción B** — descripción. Ventajas / inconvenientes.

## Decisión

Qué se decide, en una frase, y por qué.

## Consecuencias

- **Positivas:** ...
- **Negativas / coste asumido:** ...
- **Condición de revisión:** qué tendría que pasar para reabrir esta decisión.
```

## ADR existentes

| ADR | Título | Estado |
|---|---|---|
| [001](ADR-001-estructura-y-regla-de-dependencias.md) | Estructura de solución y regla de dependencias | **Aceptado** |
| [002](ADR-002-estrategia-de-precios-cierre-diario.md) | Precios: cierre diario como base, intradía como capa opcional | **Aceptado** |
| [003](ADR-003-coste-en-moneda-local.md) | Guardar el coste en su moneda original con fecha de compra | **Aceptado** |
| [004](ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md) | Cobertura de mercados (EE. UU. + Canadá) con presupuesto cero | **Aceptado** (riesgo asumido) |
| [005](ADR-005-postgresql-desde-el-inicio.md) | PostgreSQL como base de datos desde el inicio | **Aceptado** |
| [006](ADR-006-autenticacion-local-identity-cookie.md) | Autenticación local con ASP.NET Core Identity y cookie | **Aceptado** |
| [007](ADR-007-minimal-apis-sin-mediador.md) | Minimal APIs con manejadores directos, sin mediador en proceso | **Aceptado** |
| [008](ADR-008-lotes-por-compra.md) | Lotes por compra desde el MVP (coste convertido lote a lote) | **Aceptado** |
| [009](ADR-009-carga-inicial-lote-agregado.md) | Carga inicial con un lote agregado y fecha declarada | **Aceptado** |
| [010](ADR-010-se-trabaja-con-lo-que-hay.md) | Se trabaja con los estados disponibles (sin historial anterior ni tres cuentas) | **Aceptado** |
| [011](ADR-011-coste-al-cambio-de-hoy.md) | El coste se convierte al cambio de hoy, no al de la compra | **Aceptado** |
| [012](ADR-012-administrador-lectura-portafolios.md) | El administrador puede ver los portafolios de los demás (solo lectura) | Sustituido por [ADR-014](ADR-014-administrador-escritura-con-auditoria.md) |
| [013](ADR-013-interfaz-multi-idioma.md) | Interfaz multi-idioma (español, francés e inglés) | **Aceptado** |
| [014](ADR-014-administrador-escritura-con-auditoria.md) | El administrador puede modificar datos ajenos, con auditoría | **Aceptado** |
