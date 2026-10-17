# ADR-007 — Minimal APIs con manejadores directos, sin mediador en proceso

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

## Contexto

Un solo desarrollador, ~20 casos de uso previstos, Clean Architecture obligatoria. Se evaluó el uso
de un mediador en proceso (MediatR), que es el patrón habitual en las plantillas de Clean
Architecture en .NET.

Hecho relevante y reciente: **MediatR pasó a licencia dual** (RPL 1.5 o licencia comercial) con su
versión 13. La edición comunitaria es gratuita para personas y empresas por debajo de 5 M USD de
ingresos anuales, pero **exige registrar una clave de licencia**. Es decir: a esta escala el coste en
dinero sería cero, pero no el coste en fricción y en dependencia.

## Opciones consideradas

1. **Minimal APIs con manejadores invocados directamente desde los endpoints.** Cada caso de uso es
   una clase con una única responsabilidad, llamada explícitamente.
2. **Minimal APIs con MediatR.** Añade el mediador y su pipeline de behaviors.
3. **Controllers con MediatR.** El estilo más extendido en tutoriales y plantillas; más ceremonia.

## Decisión

**Opción 1: Minimal APIs agrupadas por área, con manejadores directos.**

Motivos:

- Con ~20 casos de uso, un mediador cambia una llamada directa por una indirección que hay que
  aprender a seguir en el depurador. El beneficio principal del pipeline (validación, logging,
  transacciones en un solo sitio) **no compensa el coste a esta escala**.
- Ese pipeline se puede obtener sin dependencia nueva: validación en el endpoint, logging en
  middleware y transacción en un decorador del `IUnitOfWork`.
- Añadir a un proyecto personal una dependencia con gestión de clave de licencia es fricción
  innecesaria, y va en dirección contraria a la restricción de simplicidad del proyecto.

## Consecuencias

- **Positivas:** flujo legible de arriba abajo; menos paquetes; sin registro de licencias; depuración
  directa; los casos de uso siguen siendo clases de responsabilidad única y **igual de testeables**
  que con mediador (se instancian y se invocan).
- **Negativas / coste asumido:** si el proyecto llegara a centenares de casos de uso, la repetición de
  validación, logging y transacción en cada endpoint empezaría a notarse y habría que extraer un
  pipeline propio. Es un coste **hipotético**, no actual, y la salida no requiere rehacer el diseño.
- **Aclaración importante:** esto **no** descarta CQRS como criterio de diseño. Se siguen separando
  lecturas y escrituras en manejadores y DTOs distintos (`GetPortfolioViewQuery` frente a
  `AddPositionCommand`). Lo que se descarta es **el bus en proceso**, no la separación.
- **Condición de revisión:** superar ~100 casos de uso, o incorporar un segundo desarrollador al
  proyecto.
