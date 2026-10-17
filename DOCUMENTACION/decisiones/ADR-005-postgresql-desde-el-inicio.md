# ADR-005 — PostgreSQL como base de datos desde el inicio

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

## Contexto

2–5 usuarios, 20–60 posiciones, despliegue local primero con Docker Compose y alojamiento contratado
más adelante. La aplicación es financiera, así que la **precisión decimal no es un detalle**: los
importes, las cantidades y los tipos de cambio se acumulan en totales y cualquier pérdida de
precisión se propaga a todos los números que ve el usuario.

Se evaluaron SQLite y PostgreSQL, y se consideró la vía intermedia de empezar con uno y migrar.

## Opciones consideradas

1. **SQLite.** Un archivo, respaldo = copiarlo, cero administración. Limitación concreta: **SQLite no
   tiene tipo decimal nativo**, así que EF Core almacena los `decimal` como texto para no perder
   precisión, y en consecuencia la **ordenación y la agregación sobre esos campos quedan fuera del
   alcance de SQL**. En una aplicación financiera eso es una limitación latente que aparece justo
   cuando se quiere una consulta agregada (suma de valor por moneda, por ejemplo).
2. **PostgreSQL.** Tipos `NUMERIC` nativos, agregaciones en SQL disponibles, herramientas de respaldo
   y restauración maduras, inspección con cualquier cliente. A cambio: un servicio más en el
   `compose.yml` y una tarea de respaldo que administrar.
3. **SQLite ahora, PostgreSQL al contratar alojamiento.** Reparte la decisión en el tiempo. Se
   descarta porque, ya que se iba a usar Docker de todos modos, añadir el contenedor de PostgreSQL
   hoy cuesta casi lo mismo que añadirlo después, y migrar una base en producción es trabajo sin
   beneficio.

## Decisión

**Opción 2: PostgreSQL desde el inicio**, ejecutado en contenedor con la versión mayor fijada y el
volumen de datos en una ruta persistente del host.

Se descarta SQLite a pesar de su simplicidad operativa porque la limitación con los decimales
afectaría precisamente al tipo de consulta que esta aplicación va a necesitar.

**Se descarta explícitamente usar seguridad a nivel de fila (RLS) de PostgreSQL** para el aislamiento
entre usuarios. Con cinco personas conocidas, el filtro global de consulta más la comprobación
explícita en cada caso de uso son suficientes, más simples de razonar y más fáciles de probar. Añadir
RLS sería complejidad empresarial en un proyecto que pide justo lo contrario.

## Consecuencias

- **Positivas:** precisión decimal nativa; agregaciones y ordenaciones en SQL disponibles si algún
  día conviene hacer el cálculo en la base en vez de en memoria; camino despejado si el proyecto
  crece o si la base se separa del servidor de aplicaciones.
- **Negativas / coste asumido:** hay un servicio más que mantener. En concreto:
  - la **versión mayor de la imagen se fija** y las actualizaciones son una decisión planificada, no
    automática (una actualización accidental de la imagen puede requerir migración de datos);
  - el respaldo deja de ser "copiar un archivo" y pasa a ser un `pg_dump` con retención, y **hay que
    probar una restauración al menos una vez** (ver pregunta E3);
  - el volumen de datos debe vivir en una ruta real del host, **nunca dentro del contenedor**, o se
    pierde al recrearlo.
- **Nota sobre la advertencia de decimales:** desaparece. La aritmética financiera se sigue haciendo
  en C# sobre `decimal` (el backend es la única autoridad numérica), pero ya no hay ninguna razón
  técnica para evitar agregar en SQL.
- **Condición de revisión:** ninguna previsible. Esta decisión es de las más estables del proyecto.
