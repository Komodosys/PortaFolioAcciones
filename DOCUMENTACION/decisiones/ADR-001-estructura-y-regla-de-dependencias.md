# ADR-001 — Estructura de solución y regla de dependencias

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

> **Base de la aceptación (2026-10-12):** el usuario confirmó la estructura propuesta al cerrar la
> arquitectura. El stack concreto que la acompaña (.NET 10 LTS, Angular 22, PostgreSQL, Identity
> local, Minimal APIs sin mediador) está en [../07-baseline-tecnologico.md](../07-baseline-tecnologico.md).

## Contexto

El proyecto exige Clean Architecture con capas claramente separadas (dominio, aplicación,
infraestructura, API, frontend) y dependencias apuntando hacia el dominio. La lógica de negocio debe
quedar independiente de frameworks e infraestructura.

Restricciones reales del contexto: **un solo desarrollador**, 2–5 usuarios, aplicación de
seguimiento sin operaciones, y fuentes de datos externas (precios, tipos de cambio) que
previsiblemente cambiarán al menos una vez durante la vida del proyecto.

El riesgo de este tipo de proyecto no es quedarse corto de capas, sino lo contrario: montar cuatro
proyectos y seis abstracciones para una aplicación que cabe en la cabeza.

## Opciones consideradas

1. **Proyecto único con carpetas por capa.** Menos ceremonia. Pero nada impide que el dominio
   empiece a referenciar EF Core o ASP.NET, y la regla de dependencias queda solo en la disciplina
   del desarrollador.
2. **Cuatro proyectos .NET (Domain, Application, Infrastructure, Api) + workspace Angular.** La
   regla de dependencias la verifica el compilador: si Domain intenta referenciar Infrastructure, no
   compila.
3. **Proyectos por capa + separar también los adaptadores en proyectos propios** (uno por
   proveedor de precios). Máximo aislamiento. Coste alto y sin beneficio claro a esta escala.

## Decisión

**Opción 2.** Cuatro proyectos .NET — `Domain`, `Application`, `Infrastructure`, `Api` — más el
workspace Angular en `web/`, en un único repositorio.

- `Domain` no referencia ningún proyecto ni paquete de framework.
- `Application` referencia `Domain`.
- `Infrastructure` referencia `Application` (y `Domain` a través de ella) e implementa sus puertos.
- `Api` referencia `Application` e `Infrastructure` y es el único lugar donde se compone la
  aplicación (inyección de dependencias).
- Angular consume la API por HTTP y **no contiene lógica de negocio financiera**.

Se añade una **prueba automática de arquitectura** (por ejemplo con `NetArchTest`) que falle si
`Domain` referencia cualquier otra capa o cualquier framework. La regla deja de depender de que
alguien se acuerde.

## Consecuencias

- **Positivas:** las reglas de negocio (valoración, conversión, G/P) se prueban sin base de datos ni
  servidor; cambiar de proveedor de precios o de base de datos es sustituir un adaptador; la regla
  de dependencias la verifica el compilador.
- **Negativas / coste asumido:** más proyectos y más código de composición que una carpeta por capa;
  algo de fricción al añadir una funcionalidad simple. Se acepta a cambio de que la restricción
  obligatoria se cumpla por construcción.
- **Condición de revisión:** si el proyecto incorporara un segundo desarrollador o un segundo
  cliente (app móvil nativa), convendría revisar si `Application` debe exponer contratos
  compartidos. No antes.
