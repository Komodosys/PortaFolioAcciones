# 07 — Baseline tecnológico

Stack fijado el **2026-10-12**. Este documento es la referencia única del proyecto: si algo aquí
discrepa con otro documento, manda este y hay que corregir el otro.

## Versiones

| Componente | Versión | Por qué |
|---|---|---|
| Backend | **.NET 10 (LTS)** | Única LTS vigente; soporte hasta **2028-11-14** |
| Frontend | **Angular 22** | Última estable; soporte activo hasta **2027-06-30** |
| Base de datos | **PostgreSQL**, versión mayor fijada en la imagen | Ver [ADR-005](decisiones/ADR-005-postgresql-desde-el-inicio.md) |
| ORM | **EF Core 10** + Npgsql | Alineado con .NET 10 |

**Dos advertencias de versión que conviene tener presentes:**

- **No usar .NET 8 ni .NET 9.** Ambos terminan su soporte el **2026-11-10**. Un proyecto nuevo sobre
  cualquiera de los dos nacería sin soporte desde el primer día.
- **Angular 20 termina el 2026-11-28.** Angular publica una versión mayor cada 6 meses y la mantiene
  activa solo 6 meses, más 12 de soporte crítico. Consecuencia práctica: **hay que asumir una
  actualización mayor de Angular aproximadamente cada año**, de forma indefinida. No es motivo para
  no usar Angular —es una restricción del proyecto—, pero sí una carga de mantenimiento que conviene
  tener presente antes de añadir encima otros frameworks con caducidad propia.

## Decisiones de arquitectura

| Área | Decisión | ADR |
|---|---|---|
| Estructura | 4 proyectos .NET + `web/` Angular en un monorepo; dependencias hacia el dominio; test de arquitectura | [ADR-001](decisiones/ADR-001-estructura-y-regla-de-dependencias.md) |
| Precios | Cierre diario como base; intradía pospuesto a v0.2 | [ADR-002](decisiones/ADR-002-estrategia-de-precios-cierre-diario.md) |
| Divisas | Coste en moneda original + fecha de compra | [ADR-003](decisiones/ADR-003-coste-en-moneda-local.md) |
| Cobertura de datos | EE. UU. + Canadá con presupuesto cero; Yahoo para Canadá | [ADR-004](decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md) |
| Base de datos | PostgreSQL desde el inicio; sin seguridad a nivel de fila | [ADR-005](decisiones/ADR-005-postgresql-desde-el-inicio.md) |
| Autenticación | ASP.NET Core Identity local + cookie; sin registro público | [ADR-006](decisiones/ADR-006-autenticacion-local-identity-cookie.md) |
| API y orquestación | Minimal APIs con manejadores directos; sin mediador | [ADR-007](decisiones/ADR-007-minimal-apis-sin-mediador.md) |

## Librerías previstas

| Necesidad | Elección | Nota |
|---|---|---|
| Acceso a datos | EF Core + Npgsql | |
| Logging | Serilog | |
| Resiliencia HTTP | `Microsoft.Extensions.Http.Resilience` | Reintentos con espera creciente e interruptor de circuito |
| Documentación de API | OpenAPI (integrado en ASP.NET Core) | Expuesto solo en desarrollo, salvo decisión contraria |
| Pruebas | xUnit | |
| Aserciones en pruebas | **Nativas de xUnit**, o un fork libre | Aviso: FluentAssertions pasó a ser un producto comercial de Xceed, con condiciones gratuitas para uso no comercial que hay que verificar. Existe un fork comunitario con licencia Apache |
| Mapeo entre capas | Manual | Si cansa, un generador en tiempo de compilación (Mapperly). Nunca AutoMapper |
| Traducción en el frontend | Librería de **tiempo de ejecución** (Transloco o equivalente) | La i18n integrada de Angular es de compilación y **no sirve** para cambiar el idioma desde las preferencias ([ADR-013](decisiones/ADR-013-interfaz-multi-idioma.md)) |
| Migraciones de EF Core | **Paso explícito**, dentro de un **comando único de actualización** | `actualizar.cmd` hace: copia de seguridad → parar → migrar → arrancar. Así no hay orden que recordar. La aplicación **se niega a arrancar** si la base está desactualizada, como red de seguridad. Nota: en PostgreSQL el DDL es transaccional, así que una migración fallida se deshace sola |
| Proxy inverso | Caddy | Elegido sobre nginx por el certificado TLS automático cuando llegue el alojamiento; en local cumple igual |
| Contenedores | Docker Compose | |
| Orquestación de desarrollo | **No se usa .NET Aspire** | Añade framework y curva de aprendizaje para tres contenedores que Compose ya resuelve |

**Regla de higiene de dependencias:** antes de añadir cualquier paquete, comprobar su licencia
vigente. Entre 2025 y 2026 varias librerías muy extendidas del ecosistema .NET (MediatR, AutoMapper,
FluentAssertions) pasaron de código abierto a modelos comerciales o duales. Comprobarlo cuesta un
minuto; descubrirlo tarde cuesta una reescritura.

## Lo que no se usa, y por qué

- **MediatR y AutoMapper:** licencia dual con registro de clave e indirección innecesaria a esta
  escala ([ADR-007](decisiones/ADR-007-minimal-apis-sin-mediador.md)).
- **.NET Aspire:** resuelve orquestación de aplicaciones distribuidas; aquí hay tres contenedores.
- **Dapper:** duplicaría el modelo de acceso a datos sin ningún problema de rendimiento que resolver.
- **Seguridad a nivel de fila de PostgreSQL:** complejidad empresarial para cinco usuarios conocidos
  ([ADR-005](decisiones/ADR-005-postgresql-desde-el-inicio.md)).

## Pendiente de decidir (deliberadamente pequeño)

Ninguna de estas bloquea el arranque:

- **Validación:** DataAnnotations (sin dependencia) o FluentValidation (libre, bajo la .NET
  Foundation). Recomendación: empezar con DataAnnotations y cambiar si las reglas se complican.
- **Restablecimiento de contraseña por el administrador:** el [ADR-006](decisiones/ADR-006-autenticacion-local-identity-cookie.md)
  exige que exista, pero no detalla el flujo.
- **Migraciones de EF Core:** aplicarlas automáticamente al arrancar es cómodo y peligroso. Un
  proyecto familiar se lleva mejor con un paso explícito.
- **Formatos de fecha y número**, y si la interfaz será solo en español.
