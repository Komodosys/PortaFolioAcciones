# 03 — Arquitectura por capas y estructura de la solución

## Principio rector

Una sola regla gobierna todo lo demás: **las dependencias apuntan hacia dentro**. La capa de
dominio no conoce Entity Framework, ni HTTP, ni ASP.NET, ni la base de datos. La aplicación conoce
el dominio. La infraestructura y la API conocen la aplicación. Nunca al revés.

El objetivo práctico no es el dibujo: es que las reglas de negocio (valoración, conversión,
cálculo de ganancia/pérdida) se puedan probar sin levantar una base de datos ni un servidor, y que
cambiar de proveedor de precios o de base de datos sea sustituir un adaptador.

```
                 ┌──────────────────────────────┐
   HTTP/JSON     │  PortaFolioAcciones.Api      │  ← composición, auth, endpoints
                 └──────────────┬───────────────┘
                                │ depende de
                 ┌──────────────▼───────────────┐
                 │ PortaFolioAcciones.Application│ ← casos de uso, DTOs, puertos
                 └──────────────┬───────────────┘
                                │ depende de
                 ┌──────────────▼───────────────┐
                 │ PortaFolioAcciones.Domain     │ ← entidades, valores, reglas
                 └──────────────────────────────┘
                                ▲
                 ┌──────────────┴───────────────┐
                 │ PortaFolioAcciones.Infrastructure │ ← EF Core, HTTP, caché, jobs
                 └──────────────────────────────┘
                    (implementa puertos; se inyecta desde la API)

  Angular (web/)  ──HTTP──▶  Api        (el frontend es un consumidor, no una capa del backend)
```

## Estructura de carpetas propuesta

```
/
├─ DOCUMENTACION/                       ← esta carpeta
├─ src/
│  ├─ PortaFolioAcciones.Domain/
│  │  ├─ Instruments/                   ← Instrument, AssetType, PricingFrequency, Ticker
│  │  ├─ Portfolios/                    ← Portfolio, Position
│  │  ├─ Valuations/                    ← Money, Currency, FxRate, MoneyConverter
│  │  ├─ Pricing/                       ← PricePoint, IPrecioProvider (puerto), ManualPrice
│  │  └─ Users/                         ← User (mínimo: identidad y moneda base)
│  ├─ PortaFolioAcciones.Application/
│  │  ├─ Portfolios/                    ← Crear/editar portafolio, alta de posiciones
│  │  ├─ Queries/                       ← Vista de portafolio, detalle de posición
│  │  ├─ Pricing/                       ← Orquestación de actualización de precios y FX
│  │  └─ Abstractions/                  ← IUnitOfWork, IClock, ICurrentUser
│  ├─ PortaFolioAcciones.Infrastructure/
│  │  ├─ Persistence/                   ← DbContext, configuraciones EF, migraciones
│  │  ├─ Pricing/                       ← Proveedores concretos (adaptadores)
│  │  ├─ Fx/                            ← Proveedor de tipos de cambio
│  │  └─ Jobs/                          ← Job diario de precios, refresco intradía
│  └─ PortaFolioAcciones.Api/
│     ├─ Endpoints/                     ← Un archivo por área (minimal APIs agrupadas)
│     ├─ Security/                      ← Autenticación, autorización, usuario actual
│     └─ Program.cs
├─ tests/
│  ├─ PortaFolioAcciones.Domain.Tests/          ← valoración, FX, redondeo (prioridad alta)
│  ├─ PortaFolioAcciones.Application.Tests/     ← casos de uso con dobles
│  ├─ PortaFolioAcciones.Api.Tests/             ← integración: aislamiento entre usuarios
│  └─ PortaFolioAcciones.TestSupport/
└─ web/                                 ← workspace Angular
   └─ src/app/
      ├─ core/                          ← servicios HTTP, interceptores, guards, modelos
      ├─ features/portfolio/            ← tabla de posiciones, totales
      ├─ features/positions/            ← formularios de alta/edición
      └─ shared/                        ← componentes de presentación
```

Que Angular viva en `web/` dentro del mismo repositorio es una recomendación, no una obligación:
con 2–5 usuarios y un solo desarrollador, un repositorio evita desincronizar contratos de API.

## Qué vive en cada capa

### Domain (sin dependencias de framework)

- **Entidades:** `Portfolio`, `Position`, `Instrument`, `User`.
- **Objetos de valor:** `Money` (importe + moneda, nunca un `decimal` suelto), `Currency` (código
  ISO 4217), `Quantity`, `Percentage`, `Ticker`, `AssetType`, `PricingFrequency`, `FxRate`.
- **Servicios de dominio puros:** valoración de una posición, cálculo de ganancia/pérdida,
  conversión de moneda, agregación del portafolio. Reciben todo por parámetro; no consultan nada.
- **Puertos que el dominio necesita:** `IInstrumentRepository`, `IPortfolioRepository`,
  `IPriceProvider`, `IFxRateProvider`, `IClock`. Son interfaces; las implementaciones viven fuera.

El criterio para decidir si algo entra en Domain: *¿cambiaría esta regla si cambiara la base de
datos, el framework web o el proveedor de precios?* Si la respuesta es no, es dominio.

### Application

- Casos de uso: un manejador por operación (`GetPortfolioViewQuery`, `AddPositionCommand`,
  `RefreshPricesCommand`). Orquestan dominio + puertos, no contienen reglas de negocio.
- DTOs de salida ya pensados para la pantalla (la vista de portafolio se devuelve calculada, no se
  recalcula en Angular).
- `ICurrentUser` y `IClock` como abstracciones: son las dos cosas que hacen que los tests de
  aislamiento y los cálculos de fecha sean deterministas.

### Infrastructure

- EF Core + proveedor de base de datos, configuraciones `IEntityTypeConfiguration`, migraciones.
- Adaptadores de precios y de tipos de cambio (uno por proveedor).
- Caché en memoria (`IMemoryCache`) para evitar repetir llamadas.
- Jobs en segundo plano (`IHostedService` + planificador simple, sin necesidad de Quartz/Hangfire
  en el MVP).
- Logging estructurado (Serilog) y resiliencia HTTP (Polly: reintentos con espera creciente).

### Api

- Minimal APIs agrupadas por área, con `TypedResults` y validación.
- Autenticación y autorización; el usuario nunca se recibe por parámetro de la URL, se obtiene del
  contexto autenticado.
- Manejo uniforme de errores con `ProblemDetails`.
- OpenAPI/Swagger en desarrollo para que Angular tenga un contrato visible.

### Frontend (Angular)

- Componentes standalone + signals; estructura por funcionalidad, no por tipo de archivo.
- `HttpClient` con interceptores (errores, credenciales).
- **Sin lógica de negocio financiera.** Sumar, convertir y calcular porcentajes es responsabilidad
  del backend. El frontend formatea y presenta. Esto evita que dos implementaciones del mismo
  cálculo (C# y TypeScript) se desincronicen en los redondeos.

## Decisiones técnicas cerradas (2026-10-12)

Todas las filas de esta tabla están **resueltas**. El resumen operativo del stack está en
[07-baseline-tecnologico.md](07-baseline-tecnologico.md).

| # | Decisión | Resuelto | Motivo breve |
|---|---|---|---|
| A | Dónde viven las interfaces de repositorio | **Domain** para los agregados; **Application** para puertos de lectura propios de un caso de uso | Es la lectura literal de la regla de dependencias y no cuesta nada a esta escala |
| B | Mediador de casos de uso | **Sin mediador** ([ADR-007](decisiones/ADR-007-minimal-apis-sin-mediador.md)) | Con ~20 casos de uso la indirección no compensa; MediatR además tiene licencia dual con registro de clave |
| C | Mapeo entre capas | **Manual** (o Mapperly si cansa) | Un error de mapeo debe aparecer al compilar, no en ejecución; y AutoMapper arrastra el mismo problema de licencia |
| D | Acceso a datos | **EF Core para todo** | Con 5 usuarios no hay problema de rendimiento que justifique Dapper |
| E | Estilo de API | **Minimal APIs** agrupadas por área ([ADR-007](decisiones/ADR-007-minimal-apis-sin-mediador.md)) | Menos ceremonia, mismo resultado |
| F | Autenticación | **ASP.NET Core Identity + cookie**, sin registro público ([ADR-006](decisiones/ADR-006-autenticacion-local-identity-cookie.md)) | Aplicación privada; JWT y OAuth añaden piezas sin resolver ningún problema real en este escenario |
| G | Base de datos | **PostgreSQL** desde el inicio ([ADR-005](decisiones/ADR-005-postgresql-desde-el-inicio.md)) | Tipos `NUMERIC` nativos; en SQLite los decimales se almacenan como texto y no admiten agregación en SQL |
| H | Pruebas de frontend | **Mínimas** | Si el frontend no tiene lógica de negocio, el esfuerzo rinde mucho más en los tests de dominio |

## Riesgos de la organización propuesta

- **Sobre-ingeniería por capas.** Con un solo desarrollador, cuatro proyectos .NET más Angular
  pueden sentirse pesados. La mitigación es de disciplina, no de estructura: no crear carpetas
  vacías "por si acaso" ni abstracciones sin una segunda implementación real.
- **Duplicación de cálculos.** El riesgo real es calcular la ganancia en el backend y "ajustarla" en
  Angular. Regla explícita: el backend es la única autoridad numérica.
- **El dominio se contamina.** Es lo que pasa cuando EF Core o ASP.NET se referencian desde Domain.
  Conviene una prueba automática de arquitectura (por ejemplo, con `NetArchTest`) que falle si
  Domain referencia cualquier otra capa. Es barato y protege la decisión durante años.

## Despliegue (decidido el 2026-10-12)

El usuario ha decidido **ejecutar en local por ahora**, con intención de contratar alojamiento más
adelante. Esto no cambia la arquitectura, pero sí cuatro cosas concretas:

- **Docker Compose desde el primer día**, aunque se ejecute en local: API, base de datos y proxy
  inverso descritos en un archivo. El día de la mudanza al hosting, el despliegue es copiar el
  `compose.yml` y el volumen de datos, no rehacer nada.
- **PostgreSQL en contenedor**, con la versión mayor fijada en la imagen y el volumen de datos en una
  ruta real del host (nunca dentro del contenedor). Consecuencia directa: el respaldo **deja de ser
  "copiar un archivo"** y pasa a ser un `pg_dump` con retención, que hay que automatizar y cuya
  restauración hay que **probar al menos una vez** ([ADR-005](decisiones/ADR-005-postgresql-desde-el-inicio.md)).
- **HTTPS no es obligatorio en local, la autenticación sí.** Los miembros de la familia acceden por
  la red local, pero conviene no diseñar nada que dependa de "estar en una red de confianza": el
  usuario actual siempre se obtiene del contexto autenticado, nunca de un parámetro ni de un valor
  por defecto.
- **El job diario de precios corre en el mismo proceso**, así que la aplicación debe poder
  registrarse como servicio del sistema para que la actualización no dependa de que alguien abra la
  aplicación.

Cuando se contrate alojamiento aparecerá trabajo que hoy no existe: certificado TLS, actualizaciones
de seguridad y decidir si se expone a Internet o se accede por VPN. Es el momento de cerrar E2 y E3
de [06-preguntas-abiertas.md](06-preguntas-abiertas.md).
