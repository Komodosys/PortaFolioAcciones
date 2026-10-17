# TODO — PortaFolioAcciones

**Última actualización:** 2026-10-12
**Estado:** diseño cerrado, **cero líneas de código de la aplicación**. Lo que existe hoy son
decisiones documentadas y la cadena de herramientas que **extrae, valida y reconcilia la cartera real**.

Marca `[x]` al completar. Añade al final de *Hecho* una entrada con la fecha cuando cierres un bloque,
para que este archivo haga de diario del proyecto y no solo de lista de pendientes.

**Definición de "producto funcional"**: una aplicación que las 2–5 personas de la familia puedan abrir,
autenticarse cada una con su cuenta, ver sus propias posiciones valoradas en CAD con precios
actualizados a diario, y consultar su ganancia/pérdida — con respaldo automático que alguien haya
probado restaurar al menos una vez.

---

## Qué queda pendiente, de un vistazo

| # | Qué | Quién | Tamaño |
|---|---|---|---|
| 1 | **Arrancar el andamiaje** — Fase 1: solución .NET, Angular, Docker Compose | yo | 🟡 |
| 2 | Dominio, modelo de datos y sus tests — Fase 2 | yo | 🔴 |
| 3 | Autenticación, casos de uso, API y frontend — Fases 3 a 6 | yo | 🟡 |
| 4 | Desplegar, respaldar y verificar contra el bróker — Fase 7 | yo | 🔴 |
| 5 | ~~Instalar Docker Desktop~~ | **hecho** | ✅ |

**Nada se bloquea salvo el punto 1**, y **ya no queda ningún dato pendiente por tu parte**: los estados
que faltan y las fechas de compra dejaron de ser necesarios con los ADR-010 y ADR-011.

**Cerrado el 2026-10-12:** no habrá estados de las otras tres cuentas ni de meses anteriores a 2026
([ADR-010](decisiones/ADR-010-se-trabaja-con-lo-que-hay.md)), y el coste se convierte **al cambio del
día**, de modo que **las fechas de compra dejan de ser necesarias** y desaparece toda aproximación
([ADR-011](decisiones/ADR-011-coste-al-cambio-de-hoy.md)).

## Lo que necesito de ti (el detalle)

| # | Qué | Por qué |
|---|---|---|
| 1 | **Crear el repositorio en tu cuenta de GitHub y darme la dirección del remoto** | El repositorio local **ya está creado** (`git init`, rama `main`) y **ya está anonimizado y comprobado**. Falta el remoto y el primer envío. **Mándame la URL y lo subo**, o súbelo tú: `git remote add origin <url>` y `git push -u origin main` |

### La publicación: hecho y comprobado

El repositorio nació con datos financieros reales y **se publica**, así que se hizo una pasada de
anonimización y se dejó una comprobación automática:

- **Fuera del repositorio los datos reales**: estados de cuenta, texto extraído, los CSV de posiciones y
  lotes, la lista de instrumentos probados y la lista de cadenas a vigilar. Los CSV **no se pierden**:
  se regeneran volviendo a ejecutar la extracción sobre los estados de cuenta
- **Anonimizado lo que sí se publica**: los nombres de persona son `Titular A` y `Titular B`, y los
  números de cuenta son `RRSP-1`…`TD-1`. **133 sustituciones en 13 ficheros**. Las **cifras siguen siendo
  las reales**, porque son las que hacen que las pruebas demuestren algo
- **Comprobación automática** en
  [`herramientas/comprobar-datos-personales`](../herramientas/comprobar-datos-personales/comprobar.ps1):
  busca las cadenas prohibidas en **los ficheros que de verdad se publicarían** —le pregunta a git— y
  devuelve `1` si encuentra algo. Existe porque **anonimizar una vez no basta**: cualquier línea nueva
  puede volver a colar un nombre y nadie lo notaría hasta que estuviera publicado y clonado
- **Comprobado**: 23 cadenas vigiladas, 210 ficheros, **ninguna aparición**
- **Y un fallo que la anonimización provocó y que solo se vio al ejecutar**: sustituir un nombre de pila por
  su etiqueta lo hizo **también dentro de los identificadores**: `carteraDeNombre` se convirtió en
  `carteraDeTitular B`, con un espacio dentro. El código `.cs` no se rompió —lo habría cazado el
  compilador—, pero los scripts de prueba `.mjs` **no se compilan**: fallaban con un error de sintaxis y
  las suites no llegaron a ejecutarse. **34 identificadores reparados** con una regla precisa: pegado a
  una letra es un identificador, con un espacio delante es una etiqueta. Lección: sustituir texto en masa
  hay que **verificarlo ejecutando**, no leyendo el diff
- **Y la comprobación se ganó el sueldo el mismo día que se escribió**: al documentar ese fallo, **este
  mismo párrafo volvió a escribir el nombre real**, tres veces. La cazó ella, no una lectura. Es justo el
  caso que la justifica: **el sitio donde menos se espera un nombre real es el texto que habla de
  quitarlos**, porque ahí se escribe para explicarlo

**Docker: hecho.** Instalado el 2026-10-12 y comprobado: **29.8.2** con **Compose v5.6.0**, demonio en
pie, 12 CPU y 8 GB. PostgreSQL 18.6 ya está levantado encima.

**La Fase 1 está en marcha**, así que no queda nada que confirmar por tu parte.

---

## Hecho — 2026-10-12

### Documentación y decisiones

- [x] Ocho documentos de análisis en `DOCUMENTACION/` (inventario funcional, priorización MVP,
      arquitectura, modelo de datos, precios y divisas, preguntas abiertas, baseline, prueba de cobertura)
- [x] **Catorce ADR**, trece en estado `Aceptado` y uno sustituido, con convenciones y plantilla para los siguientes:
  - [x] ADR-001 — cuatro proyectos .NET + `web/`, dependencias hacia el dominio, test de arquitectura
  - [x] ADR-002 — cierre diario como base; intradía pospuesto a v0.2
  - [x] ADR-003 — coste en su moneda original + fecha de compra
  - [x] ADR-004 — cobertura EE. UU. + Canadá con presupuesto cero (riesgo aceptado conscientemente)
  - [x] ADR-005 — PostgreSQL desde el inicio, sin seguridad a nivel de fila
  - [x] ADR-006 — ASP.NET Core Identity local + cookie, sin registro público
  - [x] ADR-007 — Minimal APIs con manejadores directos, sin mediador en proceso
  - [x] ADR-008 — **lotes por compra desde el MVP** (coste convertido lote a lote)
  - [x] ADR-009 — **carga inicial con un lote agregado y fecha declarada** (la conversión a CAD se marca como aproximada cuando la fecha no es exacta)
  - [x] ADR-010 — **se trabaja con los estados disponibles**: sin historial anterior ni estados de tres cuentas, con la incertidumbre medida y marcada
  - [x] ADR-011 — **el coste se convierte al cambio de hoy**: el rendimiento mostrado es el del activo, y las fechas de compra dejan de ser necesarias
  - [x] ADR-012 — el administrador puede ver los portafolios de los demás *(sustituido por el 014)*
  - [x] ADR-013 — **interfaz multi-idioma** (español, francés e inglés) con preferencia por usuario
  - [x] ADR-014 — **el administrador puede modificar datos ajenos, y cada cambio queda auditado**
- [x] Baseline tecnológico como referencia única: **.NET 10 LTS, Angular 22, PostgreSQL, EF Core 10**
- [x] Criterio de decisión de cobertura fijado **antes** de ver resultados, para no reinterpretarlos

### Herramientas construidas

- [x] `herramientas/prueba-cobertura/Test-Cobertura.ps1` — comprueba cobertura **e identidad** por
      instrumento, sin claves de API, con petición de prueba previa y diagnóstico de entorno
- [x] `herramientas/prueba-cobertura/ejecutar.cmd` — lanzador que resuelve la política de ejecución
- [x] `instrumentos.csv` con plantilla y ejemplos; README con veredictos y solución de problemas
- [x] Resultados archivados en `resultados/`: primero la tanda de prueba y después la cartera real completa

### Extracción de los estados de cuenta (40 PDF, 7 cuentas, 2 titulares)

- [x] Utilidad propia de extracción y parseo en `herramientas/pdf-extract/` (`extraer.mjs` + `parsear.mjs`),
      con soporte para **Questrade en inglés y en francés** y para **TD Waterhouse**, y capaz de recorrer
      subcarpetas
- [x] **32 posiciones extraídas de 8 estados**, con **validación 15/15** contra los totales que los
      propios estados imprimen por sección — no es una lectura a ojo
- [x] Listado consolidado en [`listado.csv`](../listado.csv) (**27 posiciones en 7 cuentas**) y
      auditoría completa en [`posiciones-todos-los-estados.csv`](../posiciones-todos-los-estados.csv)
- [x] Trampas de formato resueltas: el francés usa **coma decimal en cantidad y % de cartera** pero
      **punto decimal en importes**, y millares con **espacio**; las fechas en francés no las entiende `Date`
- [x] Aparecen por fin los **fondos mutuos** que faltaban para la prueba de cobertura: el estado de TD
      contiene cuatro (TDB2560C, TDB2624C, TDB2625C, TDB2626C)
- [x] Confirmado que un fondo mutuo se identifica por **código de fondo (Fundserv)**, no por símbolo de
      mercado **ni por ISIN**: en Canadá estos fondos simplemente no tienen ISIN
- [x] La cuenta RRSP-3 tiene **dos estados (enero y mayo de 2026)**; el consolidado usa el más reciente

### Cobertura de la cartera real: cerrada

- [x] **19 de 19 instrumentos cubiertos**, fondos mutuos incluidos → [ADR-004](decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md) **verificado, sin coste**
- [x] Localizados los 4 fondos de TD en la fuente y **verificados por NAV** contra el estado de cuenta
      (coinciden al tercer decimal); equivalencias guardadas en `herramientas/pdf-extract/mapa-instrumentos.csv`
- [x] **Corregido un supuesto mío:** en Canadá los fondos de Fundserv **no tienen ISIN**, y su código de
      fondo **no se indexa** en la fuente de precios. La equivalencia con la fuente hay que resolverla a
      mano una vez por instrumento, no se puede automatizar
- [x] Detectada la trampa de las series: «TD Canadian Equity Pool **Series W**» y «…**Class** Series W»
      son instrumentos distintos, con NAV 22,68 y 28,01
- [x] Confirmado que un instrumento puede **renombrarse** en la fuente (`MGK`): la comprobación de
      identidad debe avisar, no bloquear

### Verificación de una posición de principio a fin (NFLX)

- [x] Confirmado contigo: **20 acciones a 89,00 USD**, idéntico al estado de cuenta (20 × 89,00 =
      1.780,00 USD). El split 10:1 de 2025-11-17 **ya venía ajustado**, así que la alerta no aplicaba
- [x] Encontrada la **fecha de compra** en la sección de actividad del estado: **2026-01-20**
- [x] **Cadena completa de cálculo validada con datos reales y a coste cero:** coste 1.780,00 USD ×
      USD/CAD del 20-ene (1,3851) = **2.465,48 CAD**; valor hoy 1.412,20 USD × USD/CAD de hoy (1,4245) =
      **2.011,68 CAD**; resultado **−453,80 CAD (−18,41 %)**
- [x] **El efecto divisa es material y se ve:** en USD la posición cae un 20,66 %, pero en CAD solo un
      18,41 %, porque el dólar se apreció un 2,84 % frente al CAD. Justifica por sí solo guardar el
      coste en moneda original con su fecha
- [x] Detectado que la sección de actividad **también contiene ventas** (ASML, VFV): las posiciones
      extraídas son posteriores a esas operaciones
- [x] **Falsificada una suposición del ADR-003:** no siempre hay *una* fecha de compra. Hay posiciones
      de varias compras con fechas distintas (MSFT), y la fecha solo es recuperable para las
      operaciones del periodo del estado

### Los estados de todo el año (32 PDF, enero a agosto de 2026)

- [x] Extraídos y analizados **32 estados** (4 cuentas × 8 meses), con **72 de 72 secciones validadas**
      contra los totales que imprimen
- [x] **26 operaciones** extraídas de las secciones de actividad y **deduplicadas**: el mismo hecho
      aparece en dos estados consecutivos (la sección de «operaciones liquidadas tras el cierre del mes»)
- [x] **Lotes reconstruidos invirtiendo las operaciones**: 30 lotes, y las **17 posiciones cuadran** con
      el estado en cantidad y en coste
- [x] **Resuelto el caso de VEEV:** no son 3 lotes sino **4** (10 + 7 + 10 + 7 = 34 acciones, 5.628,44 USD,
      exactamente el coste que declara el estado), y la venta de 11 del 2026-08-31 deja **23**
- [x] **Dos splits reales en la cartera**, ambos declarados por la fuente: **NFLX 10:1** (2025-11-17) y
      **MGK 5:1** (abril de 2026). El de MGK se detectó por el salto de 12 a 60 acciones con el coste intacto
- [x] Resuelta la pregunta abierta del ADR-008: **una venta reparte la reducción proporcionalmente**
      entre los lotes, que es el método de coste medio del bróker. Vender 11 de 34 VEEV deja el coste
      por acción exactamente en el que declara el estado
- [x] Detectado que el estado puede mostrar la cantidad **previa a la liquidación** de una venta
      (VEEV: 34 en la tabla, 23 segregadas): hay que usar la cantidad efectiva
- [x] `listado.csv` actualizado a la **foto de agosto de 2026** en las 4 cuentas de Titular A, conservando
      enero y mayo para las otras tres; y nuevos entregables `lotes.csv`, `actividades.csv` y
      `reconciliacion.csv`
- [x] **Medido cuánto aportan los lotes de 2026**: menos de un **1 %** en cuatro posiciones reales
      (MSFT −0,50 %, MGK −0,63 %, VEEV +0,79 %, ASML −0,19 %), porque el USD/CAD solo se movió un 3,4 %
      en todo 2026. **En cambio la fecha del lote anterior a 2026 pesa hasta un 8,9 %.** Conclusión: se
      conservan los lotes de 2026 porque no cuestan nada y son invisibles en la interfaz, pero **el
      esfuerzo va a las fechas de los 15 anteriores**. Queda medido en el
      [ADR-009](decisiones/ADR-009-carga-inicial-lote-agregado.md) para no volver a decidirlo

### Verificaciones con datos reales

- [x] **Cobertura de la primera tanda: 8 de 8** (acciones y ETF de EE. UU. y Canadá)
- [x] Los fondos mutuos canadienses **sí están cubiertos**, pero con símbolos internos opacos tipo
      `0P0001K312.TO`; su código Fundserv no se indexa y no tienen ISIN (ver *Cobertura de la cartera
      real*, arriba)
- [x] En fondos, la fuente anuncia un precio «actual» que en realidad es el NAV del día anterior
      → regla fijada: leer el **último cierre no nulo** de la serie
- [x] Los fondos **no tienen intradía** y la fuente lo declara (`validRanges` sin `1d`/`5d`)
      → evidencia dura a favor del ADR-002
- [x] **No hay cotizaciones agrupadas** (el endpoint por lotes responde 401) → una petición por
      instrumento; el intradía serían ~1.560/día sin válvula de escape
- [x] `close` y `adjclose` **no son intercambiables** → `close` para valorar, `adjclose` solo para
      rentabilidad total histórica
- [x] **Calendario por bolsa demostrado en directo**: el 12/10 fue Acción de Gracias en Canadá, la TSX
      cerrada y la NYSE abierta → sin calendario, la app marcaría precios correctos como obsoletos
- [x] La fuente gratuita **entrega eventos de splits** (`events=div,splits`) → automatizarlos deja de
      necesitar un proveedor de pago; pasa de v0.3 a v0.2
- [x] Búsqueda por ISIN verificada (`US0378331005` → `AAPL`), válida para acciones y ETF; **no existe para los fondos canadienses**

### Entorno del equipo resuelto

- [x] Permisos de Windows de la carpeta del proyecto reparados y verificados (con comando de reversión
      guardado en `..\dsh-acl-recovery\`)
- [x] Política de ejecución de PowerShell diagnosticada y resuelta con lanzador
- [x] Codificación del script fijada en UTF-8 con marca de orden de bytes
- [x] **Dos fallos del propio script** detectados al ejecutarlo y corregidos: `$PSScriptRoot` vacío en
      `param()` con `-File` (rompía el lanzador) y bloqueo demasiado rígido por versión de PowerShell
- [x] **Aislamiento de procesos y MSBuild**: el equipo no deja abrir **tuberías con nombre**, que es como
      MSBuild comunica sus nodos de trabajo. El síntoma era desconcertante: `Build FAILED` con **cero
      errores** y sin ningún mensaje. Resuelto en `Directory.Build.props` con `RestoreBuildInParallel=false`
      y `UseSharedCompilation=false`, así que **no hay que pasar banderas a mano**
- [x] **VSTest no funciona en este equipo**: su `testhost` abre un handle del proceso padre para vigilar
      su salida, y eso falla con `Test Run Aborted`. Sustituido por **xunit v3 con
      Microsoft.Testing.Platform**, que ejecuta cada proyecto de pruebas como ejecutable autónomo
- [x] **Descargar paquetes NuGet necesita salida a Internet**: cuando falta, el error es
      `NU1301: No credentials are available in the security package`, que parece un problema de TLS y es
      el aislamiento. Los paquetes descargados quedan en `.nuget/` y después se compila sin red
- [x] Herramientas comprobadas: **dotnet 10.0.301** (el .NET 10 decidido), node 24.16.0, npm 11.13.0 y
      git 2.54.0. **Docker no está instalado**

### Correcciones a supuestos iniciales (documentadas)

- [x] Frankfurter ya no son «los ~30 tipos del BCE»: agrega **223 divisas de 104 bancos centrales**
- [x] **Presupuesto cero y Canadá chocan**: los planes gratuitos son casi todos solo de EE. UU.
      → origen del ADR-004
- [x] `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` **no** resuelve el bloqueo de scripts en
      este equipo; hace falta `-ExecutionPolicy Bypass` en la invocación

---

## Pendiente hasta tener producto funcional

El **camino crítico** es 0 → 1 → 2 → 3 → 4 → 5 → 6 → 7. Las fases 2 y 5 son las de riesgo real; el
resto es trabajo conocido. Tamaños orientativos: 🟢 pequeño · 🟡 medio · 🔴 grande.

### Fase 0 — Cerrar lo que falta antes de escribir código 🟢

- [x] Extraer la cartera real de los **40 estados de cuenta** → `listado.csv`, `lotes.csv`, `actividades.csv`
- [x] Ejecutar la prueba de cobertura sobre la cartera completa, **fondos mutuos incluidos**
- [x] Aplicar el criterio fijado (0–2 sin cubrir → MVP casi todo automático) y **dar el ADR-004 por verificado**
- [x] Revisar la posición de NFLX: **20 acciones a 89,00 USD**, coincide con el estado y el split ya
      venía ajustado (ver *Verificación de una posición de principio a fin*)
- [x] **Extender el extractor** para leer la **sección de actividad** y reconstruir los lotes invirtiendo
      las operaciones (`actividades.mjs`, `lotear.mjs`, `reconciliar.mjs`)
- [x] **Decidir sobre los lotes**: se conservan los 15 con fecha (cuestan cero y son invisibles en la
      interfaz) y el resto va como lote agregado; medido en el
      [ADR-009](decisiones/ADR-009-carga-inicial-lote-agregado.md)
- [x] **Cerrado:** no habrá estados de las otras tres cuentas ni de meses anteriores a 2026; se trabaja
      con lo que hay ([ADR-010](decisiones/ADR-010-se-trabaja-con-lo-que-hay.md))
- [x] **Cerrado:** con el [ADR-011](decisiones/ADR-011-coste-al-cambio-de-hoy.md) el coste se convierte
      **al cambio del día**, así que las fechas de compra dejan de ser necesarias para las cifras
      principales y **desaparece toda aproximación**
- [x] **Decisiones abiertas: todas cerradas** el 2026-10-12 (validación, formato e idioma, alcance del
      administrador, migraciones y restablecimiento de contraseña). **No queda nada pendiente por tu
      parte**

### Fase 1 — Andamiaje: que compile y se despliegue vacío ✅ *(completada el 2026-10-12)*

- [x] Solución .NET 10 (`PortaFolioAcciones.slnx`) con los cuatro proyectos y las referencias correctas
- [x] **Test de arquitectura** con **8 pruebas, verificadas falsificándolas**: se añadió Entity Framework
      al dominio y la prueba falló, como debía. Comprueba **dos capas**, y las dos hacen falta: lo que
      cada `.csproj` **declara** y lo que el ensamblado compilado **usa**. Sin la primera, declarar un
      paquete prohibido no hacía fallar nada, porque el compilador omite la referencia mientras no se use
- [x] Se descartó **NetArchTest**: no publica versión desde mayo de 2021 y su metadato de licencia viene
      vacío (regla de higiene de dependencias). Se hace con reflexión y lectura de XML, sin dependencias
- [x] `Directory.Build.props`, `.editorconfig`, `.gitignore`, `global.json` y `probar.cmd`
- [x] **xunit v3** con Microsoft.Testing.Platform: los proyectos de pruebas son ejecutables autónomos
- [x] **PostgreSQL 18.6** en `docker-compose.yml`, **levantado y verificado con consultas reales**:
      aritmética exacta (`0,1 + 0,2 = 0,3` da `true` con `NUMERIC` y `false` con coma flotante, que es
      justo la razón de haber elegido PostgreSQL) y locale `en_US.utf8` con codificación UTF8
- [x] **Montaje del directorio de datos en una carpeta del host**, fuera del contenedor y fuera del
      repositorio (`../PortaFolioAcciones-datos/postgres`): los datos sobreviven a `docker compose down`
      y se pueden respaldar desde Windows. Funciona usando el subdirectorio `PGDATA`, que evita los
      problemas de permisos típicos de los montajes desde Windows
- [x] Docker Desktop instalado y comprobado (**29.8.2**, Compose **v5.6.0**)
- [x] `docker-compose.yml` con los **tres servicios**: PostgreSQL, API y Caddy. **Verificado de punta a
      punta**: `http://localhost:8080/salud` responde `{"estado":"Healthy","comprobaciones":[{"nombre":"base-de-datos","estado":"Healthy"}]}`,
      y la cabecera `Via: 1.1 Caddy` con `Server: Kestrel` demuestra que la petición atraviesa Caddy y
      llega a la API
- [x] **Serilog** (configurado desde `appsettings.json`, sin recompilar), **ProblemDetails** (RFC 9457),
      **OpenAPI** solo en desarrollo, y endpoint de salud que **comprueba la base de datos de verdad**
- [x] **Política de migraciones aplicada**: `actualizar.cmd` hace copia → parar → migrar → arrancar, y la
      aplicación **se niega a arrancar** si hay migraciones pendientes. `dotnet-ef` instalado como
      herramienta **local** en `.herramientas/`, no global en la máquina
- [x] Workspace **Angular 22.2.2** en `web/` con la estructura `core` / `features` / `shared`, cada una
      con su README explicando qué va dentro y qué no. Compila (**239,81 kB**, 65,60 kB transferidos) y
      el dominio del frontend está separado: en `features` **no se calcula nada financiero**
- [x] **Multi-idioma funcionando de verdad** ([ADR-013](decisiones/ADR-013-interfaz-multi-idioma.md)):
      Transloco 8.4.0 (licencia MIT) con los tres idiomas cargados en tiempo de ejecución. Verificado
      sirviendo la aplicación: `/i18n/es.json`, `/fr.json` y `/en.json` responden **HTTP 200** y cada uno
      con sus textos («Posiciones» / «Positions» / «Positions», «Idioma» / «Langue» / «Language»)
- [x] Proxy de desarrollo `/api` → `http://localhost:8080`, para que el frontend llame a la API sin
      problemas de origen durante el desarrollo
- [x] **Versiones de paquetes centralizadas** en `Directory.Packages.props`, con fijado de las
      transitivas. Apareció un conflicto real: Npgsql arrastraba `EntityFrameworkCore.Relational` 10.0.4
      mientras el proyecto pedía 10.0.12, y se manifestaba como un aviso `MSB3277` que
      `TreatWarningsAsErrors` **no** convertía en error porque es de MSBuild, no del compilador. Con las
      versiones centralizadas, el aviso desaparece: `0 Warning(s), 0 Error(s)`
- [x] **El `HttpClient` quedó SIN resiliencia a propósito en esta fase**, y se dice aquí para que no
      parezca un olvido: los reintentos, el interruptor de circuito y la espera creciente se añaden en la
      **Fase 5**, junto a los proveedores de precios, que son los que de verdad se van a caer. Ponerlos
      antes habría sido envolver en reintentos una llamada que todavía no existía.
      **Se sigue en la Fase 5** — no se marca como hecho allí dos veces, se sigue allí una sola
- [x] **Pipeline mínimo de build + pruebas**, en [`.github/workflows/ci.yml`](../.github/workflows/ci.yml).
      Dejó de ser opcional el día que se decidió publicar el repositorio: mientras nadie más lo mira,
      compilar en local basta; en cuanto hay un remoto, es la red que impide subir algo roto
  - **Dos trabajos en paralelo**, API y aplicación, porque no se necesitan entre sí para compilarse ni para
    probarse: hacerlo en secuencia solo tardaría el doble
  - **La versión del SDK sale de `global.json`**, que es la referencia única. Si se cambia allí, el CI lo
    sigue solo — en vez de haber una versión escrita en dos sitios que se separan con el tiempo
  - **Los tres proyectos de pruebas se ejecutan con `dotnet run`**, apoyándose en que son ejecutables y su
    código de salida es el resultado. Así el CI no depende de que la plataforma de pruebas se entienda con
    `dotnet test`
  - **Las dependencias se cachean** entre ejecuciones: sin eso, cada empujón descarga NuGet y npm enteros,
    que es de lejos lo que más tarda
  - **Se comprueba lo mismo que se despliega**: `npm run build` es el comando que construye la imagen de
    Docker, así que un fallo de compilación se ve en el CI y no al desplegar
  - **Y se verificó antes de escribirlo**: los comandos exactos del CI se ejecutaron aquí — compilación en
    `Release` con **0 avisos**, los tres proyectos con **código de salida 0**, `npm ci` instalando 300
    paquetes, y **41 pruebas de frontend en verde**

### Fase 2 — Dominio y modelo de datos ✅ *(completada el 2026-10-13)*

- [x] **Objetos de valor** con **83 pruebas, todas en verde**: `Currency`, `Money`, `Quantity`,
      `Percentage`, `Ticker`, `FxRate` y las enumeraciones `AssetType`, `ValuationMode`,
      `PricingFrequency` y `DateAccuracy`. Con invariantes que se comprueban, no que se suponen:
  - `Money` **no deja sumar monedas distintas** (lanza excepción en vez de dar un número sin sentido)
  - `Money` **no redondea nunca solo**: el redondeo es una operación que se pide, y usa los decimales
    de la moneda (2 habitualmente, **0 en el yen**, 3 en el dinar kuwaití)
  - `Ticker` acepta **todos los símbolos reales** de los estados de cuenta, incluidos `GIB.A`,
    `0P0001K312.TO` y `TDB2624C`
  - `FxRate` no acepta tipos ≤ 0, convierte solo desde su moneda de origen y **conserva la fecha**, para
    poder auditar de dónde salió el número
- [x] **Corregido el documento [04](04-modelo-de-datos.md)**, que **contradecía al ADR-011 en cinco
      sitios**, incluida la fórmula de cálculo: seguía diciendo que el coste se convierte con el tipo de
      cambio de la fecha de cada lote. Se detectó al releerlo antes de escribir código; si se llega a
      implementar leyendo el documento, se construye justo lo que se decidió no hacer
- [x] **`Lot` y `Position`**, con **100 pruebas del dominio en verde**, verificadas **contra las cifras
      reales de los estados de cuenta**:
  - **VEEV**: los cuatro lotes reales dan 34 unidades y 5628,443 USD, y el coste por unidad (165,5424)
    coincide con el que declara el estado. Al vender 11, quedan 23 y **el coste por unidad se conserva**,
    que es LA propiedad que hace que la aplicación cuadre con el bróker en vez de inventarse otro coste
  - **MGK**: el split 5:1 real de abril de 2026 convierte 12 unidades a 201,50 en 60 a 40,30 **sin que
    cambie el coste total** (2418,00) ni la fecha de compra
  - **NFLX**: la compra real del 2026-01-20 da 20 unidades, 1780,00 USD y 89,00 por unidad
  - Al vender, **la reducción se reparte entre TODOS los lotes** y el residuo del redondeo se ajusta en
    el lote mayor, de modo que la suma vuelve a ser exacta (probado con 2/3, que no tiene decimal finito)
- [x] **Corregido el modelo con un caso que no estaba resuelto**: al venderlo todo, los lotes quedan a
      cero y **el coste histórico se perdía**, aunque la política 5 prometía que la fila cerrada lo
      conservaba. Se añadió `CostBasisAtClose` a la posición
- [x] **Las nueve entidades del modelo**: `User`, `Portfolio`, `Instrument`, `Position`, `Lot`, `Price`,
      `LatestQuote`, `PortfolioSnapshot` y `FxRate` (objeto de valor). Con dos objetos de valor que
      aparecieron al implementarlas: **`AppLanguage`** (los idiomas son una lista cerrada: falla al
      guardar un idioma sin traducciones, no al pintar la pantalla) y el conversor de moneda opcional
- [x] **Servicios de dominio puros**: `CurrencyConverter` (convierte al cambio del día y **nunca inventa
      uno**: si falta el par, lo dice), `PositionValuation` y `PortfolioValuation` con la agregación
- [x] **Políticas explícitas aplicadas**, cada una con su prueba: redondeo **solo al presentar** y con
      los decimales de la moneda (0 en el yen); **un precio tiene que ser mayor que cero**, porque un cero
      es casi siempre un dato que falta y valoraría la posición entera a cero en silencio; fecha de compra
      con su marca de exactitud; y posición cerrada que se conserva con `CostBasisAtClose`.
      *Queda para la Fase 5 el «día hábil anterior» del tipo de cambio*, que es de donde salen los datos
- [x] **Lotes**: `Position` como agregado y `Lot` por compra. Una **venta parcial reparte la reducción
      proporcionalmente** entre los lotes y **conserva el coste por unidad**, verificado con datos reales
- [x] **Tests unitarios del dominio: 199 en verde**, incluida la **verificación contra la cartera real**:
      las ocho posiciones en dólares de la cuenta RRSP-2 reproducen las cifras del estado de agosto
      (55.881,01 USD de valor, 41.671,03 de coste, 14.209,98 de ganancia y 34,10 %), y **el rendimiento
      es el mismo número en dólares y en canadienses**, que es la consecuencia del ADR-011
- [x] **Modelo de datos en PostgreSQL, aplicado y verificado**. El esquema sale con las precisiones
      exactas que se decidieron, comprobadas **en la base de datos real** y no en el código:
      `Lots.Quantity numeric(28,8)`, `Lots.CostPerUnit_Amount numeric(28,6)`,
      `Positions.CostBasisAtClose_Amount numeric(28,4)`. Los enumerados se guardan **por su nombre**
      (`Exact`, `Approximate`, `Reference`), para que la base se pueda leer a mano y para que reordenar
      un enumerado en el código no cambie el significado de lo ya guardado
- [x] **Primera migración aplicada** (`InitialSchema`): tablas `Positions` y `Lots`, con clave ajena y
      borrado en cascada
- [x] **Verificación de ida y vuelta contra PostgreSQL de verdad**, que es lo que el esquema por sí solo
      no demuestra: se escriben los cuatro lotes reales de VEEV, se leen con un contexto limpio y se
      comprueba que vuelven **enteros** —importes con su moneda, cantidades, enumerados, notas y fechas—
      y que al vender 11 el coste por unidad se conserva **después de pasar por la base de datos**
- [x] **Hallazgo de esa verificación**: los lotes volvían **en orden arbitrario**, porque la base de datos
      no garantiza ninguno sin un `ORDER BY`. Los cálculos del agregado no dependen del orden (son sumas),
      pero `Lots[0]` sí era arbitrario, y cualquier pantalla que se apoyara en él sería inestable. Ahora
      la posición los devuelve **ordenados por fecha de compra**, y lo hace el dominio
- [x] **Segunda migración aplicada** (`FullDomainModel`): las **ocho tablas** en PostgreSQL, verificadas
      una a una contra la base de datos real. La identidad del instrumento es un índice único por
      símbolo, bolsa y moneda; `Prices` y `PortfolioSnapshots` tienen clave natural compuesta; el correo
      es único solo cuando no es nulo

### Fase 3 — Autenticación y aislamiento ✅

- [x] **Identity local con cookie, sin registro público, y cuenta de administrador**, **verificado de
      punta a punta** contra la aplicación en marcha, no leyendo el código:
  - `/api/auth/me` sin sesión → **401**; con la cookie → **200** y el perfil
  - Entrar con la contraseña equivocada → **401**; con la correcta → **200** y cookie
  - Salir → **200**, y después `/api/auth/me` vuelve a **401**
  - **Bloqueo por intentos fallidos**: el quinto intento devuelve **423**, como debe
  - La cookie viaja como `httponly; samesite=lax`
- [x] **La primera cuenta de administrador se crea sola** al arrancar, y solo la primera vez. La
      contraseña se toma de la configuración o, si no está puesta, **se genera y se enseña una sola vez**
      en el registro, para no obligar a guardarla en un fichero. El perfil y la credencial **comparten
      identificador** y se crean en la misma transacción, para que no quede un perfil sin poder entrar
- [x] **La API devuelve códigos, no texto** ([ADR-013](decisiones/ADR-013-interfaz-multi-idioma.md)):
      `auth.invalid_credentials`, `auth.locked_out`, `auth.missing_fields`. El **mismo código** tanto si
      el usuario no existe como si la contraseña está mal: distinguirlos serviría para adivinar usuarios
- [x] **Una petición mal formada devuelve 400, no 500.** Lo descubrí probando: el cuerpo decía
      `"status":400` y el código HTTP era **500**. El contenido era correcto y el código mentía, que es
      peor que tener los dos mal
- [x] **Protección CSRF, verificada**: entrar sin testigo devuelve **400**, con testigo **200**, y con un
      testigo inventado **400**. Las lecturas no lo exigen, porque pedir un testigo para leer solo añade
      ruido. **`app.UseAntiforgery()` no bastaba**: se comprobó ejecutándolo y solo valida las peticiones
      de formulario, así que con esta API de JSON no protegía nada — el inicio de sesión sin testigo
      devolvía 200. La criptografía y la validación siguen siendo del marco; lo que se decide en un
      middleware propio es **a qué peticiones se les exige**, que es política, no implementación
- [x] **Un fallo traicionero que solo se veía en el registro**: `PathString.StartsWithSegments("/api/")`
      **con barra final no casa** con `/api/auth/login`, porque la barra crea un segmento vacío que no
      existe. El middleware no llegaba a ejecutarse y las peticiones pasaban sin testigo. Sin la barra,
      funciona
- [x] **Aviso de EF Core resuelto**: `AuditLog.Changes` es una colección con conversor de valor, y sin
      **comparador** EF no sabe comparar dos listas de cambios. Se añadió uno, y el aviso desapareció del
      registro de arranque. El aviso era real: sin él, EF podría no detectar modificaciones
- [x] **Creación de cuentas por el administrador**, con el perfil y la credencial **en la misma
      transacción**: si fallara la credencial, quedaría una persona que no puede entrar. El dueño de la
      cartera sale **siempre del contexto autenticado**, nunca de un parámetro
- [x] **Las dos barreras de aislamiento**, y las dos hacen falta:
  - **Primera, el filtro global de consulta** en EF Core: hace que una consulta ni siquiera *vea* las
    carteras de otra persona. Evita **olvidos**. Sin sesión no se ve nada: **falla cerrado**
  - **Segunda, la comprobación explícita** en el repositorio: aunque el filtro estuviera mal puesto o
    alguien lo desactivara, esa línea corta el paso. Evita que un filtro mal configurado se convierta en
    una **fuga silenciosa**
- [x] **El aislamiento, verificado de punta a punta** con [`herramientas/prueba-aislamiento/prueba.mjs`](../herramientas/prueba-aislamiento/prueba.mjs):
      **14 comprobaciones, 0 fallos** en la primera versión, y **21 comprobaciones, 0 fallos** con las de
      auditoría. Titular B pide la cartera de Titular A y recibe **404 — no 403**, porque
      un 403 ya revelaría que existe. Su listado trae solo la suya. El administrador ve las dos y recibe
      **200**. Sin sesión, **401**. Y una persona normal que intenta crear cuentas recibe **403**
- [x] **Un fallo que habría cometido cualquier cliente**: el testigo CSRF está **ligado a quien lo pide**,
      así que el que se pide estando anónimo deja de valer en cuanto se entra. La primera petición que
      cambiara algo después de entrar fallaba con `csrf.invalid` **aunque la sesión funcionara**, que es
      un error desconcertante. Ahora **el inicio de sesión devuelve el testigo nuevo**, para que el
      cliente no tenga que acordarse
- [x] **Restablecimiento de contraseña por el administrador**, **verificado**: temporal **mostrada una sola
      vez**, que **caduca a las 48 h**, que **obliga a cambiarla** al entrar, y que **invalida las sesiones
      abiertas**. Sin correo: se entrega en mano
  - **Lo que no puede faltar de las cuatro cosas**: sin mostrarla una vez, el administrador se queda con
    una contraseña que conoce para siempre; sin caducidad, una temporal se vuelve permanente; sin
    obligar a cambiarla, se queda puesta y la conoce alguien más que su dueño; sin invalidar sesiones,
    cambiar la contraseña **no echa a quien ya estaba dentro**, que es justo cuando uno cree haber
    arreglado el problema
  - Comprobado: la sesión anterior de Titular B recibe **401** en cuanto se restablece. Y que la temporal
    **deja de servir** después de cambiarla
  - Se comprueba el testigo de seguridad en **cada petición** (`ValidationInterval` a cero) y no cada
    media hora, que es lo que viene por defecto. Cuesta una consulta por petición y merece la pena
- [x] **El libro también registra las operaciones con contraseñas.** Se descubrió al ir a verificarlo: el
      interceptor solo miraba entidades del dominio, y restablecer una contraseña toca la tabla de
      Identity. Un libro que no registra «el administrador restableció la contraseña de Titular B» se está
      perdiendo algo importante. Ahora el interceptor también mira la cuenta, y **oculta los secretos**:
      `PasswordHash` y `SecurityStamp` se registran como `(oculto)`. Comprobado que hay **0 campos
      secretos en claro**. Guardar el hash nuevo en una tabla pensada para consultarse sería trasladar el
      secreto a donde no está protegido
- [x] **Y un defecto de calidad que se vio en el propio libro**: Identity marca **todas** las columnas de
      la cuenta como modificadas cada vez que guarda, así que un restablecimiento dejaba **16 campos**
      —`Email: null → null`, `AccessFailedCount: 0 → 0`— con los dos que importaban enterrados entre el
      ruido. Ahora solo se registra lo que **cambió de verdad**: de 16 campos a 2. Un registro lleno de
      nada es un registro que nadie lee
- [x] **Libro de auditoría: la tabla y la regla que lo hace de solo añadir**, verificadas de verdad. La
      protección <b>no está en el código de la aplicación</b>: un disparador de PostgreSQL **rechaza**
      `UPDATE` y `DELETE`, y probé los dos contra la base de datos real. Con `DO INSTEAD NOTHING` el
      intento se ignoraría en silencio y quien lo intentara creería haberlo conseguido; con `RAISE
      EXCEPTION` falla ruidosamente y dice qué operación y qué tabla
- [x] **El detalle de los cambios va en `jsonb`**, con el valor anterior y el nuevo de cada campo. Sin
      clave ajena hacia la entidad auditada: si tuviera cascada, borrar una posición se llevaría por
      delante justo la prueba de que existió
- [x] **El interceptor de EF Core que escribe el libro**, y **verificado contra la base de datos real**.
      El administrador renombra la cartera de Titular B, y el libro guarda
      `Name: "Cartera de Titular B" → "Cartera de Titular B (corregida)"`
  - Va en un **interceptor** y no en una llamada dentro de cada caso de uso, y eso es parte de la
    decisión: una auditoría que depende de que alguien se acuerde de escribirla **no es una auditoría**,
    porque el día que se olvide no habrá forma de saberlo. Se escribe en el **mismo `SaveChanges`** que
    el cambio: si el cambio se guarda, el registro se guarda
  - **Solo datos ajenos**: comprobado que hay **0 registros** donde el actor es el propio dueño. Titular B
    renombrando su cartera **no** se audita; cada uno con lo suyo
  - El dueño se resuelve **subiendo por la cadena de propiedad**: un lote es de quien es su posición, y
    una posición de quien es su cartera. El catálogo compartido (instrumentos, precios) no es de nadie,
    así que no se le atribuye un dueño
  - Se guardan **el valor anterior y el nuevo**. Un registro que dijera «se modificó la posición» no
    serviría para nada

### Fase 4 — Casos de uso y API ✅

- [x] **CRUD de portafolios completo**: crear, listar, ver, renombrar, archivar, desarchivar, cambiar
      la moneda base y reordenar. Verificado en
      [`herramientas/prueba-api/carteras.mjs`](../herramientas/prueba-api/carteras.mjs):
      **29 comprobaciones, 0 fallos**
  - **Se archiva y no se borra**: una cartera tiene posiciones e histórico, y borrarla se llevaría por
    delante justo lo que hace falta para entender qué pasó. Archivar la esconde del listado normal, pero
    sigue existiendo
  - **La moneda base se puede poner Y quitar**: mandar nula vuelve a heredar la del perfil. Poder
    deshacer la elección importa, porque si no una decisión de una vez se quedaría fija para siempre y
    cambiar la moneda del perfil dejaría de afectarle. Y el listado dice si cada cartera **hereda o
    tiene** su moneda, porque no es lo mismo
  - **Todas las modificaciones pasan por un único sitio** (`ModificarAsync`): así la comprobación de
    acceso está escrita una vez, y no hay forma de añadir una operación nueva y olvidarse de protegerla
- [x] **Una distinción que se aclaró al probar**: `Currency` acepta **cualquier código de tres letras
      bien formado** —el proveedor cubre 223 divisas y no hay lista cerrada—, así que `XYZ` («sin
      moneda») no se rechaza y lo resuelve la capa de tipos de cambio marcándolo como faltante. Lo que se
      rechaza es lo que **no tiene forma de código**: dos letras, más de tres, o dígitos. La prueba se
      corrigió para comprobar exactamente eso
- [x] **CRUD de posiciones**: alta, compras, ventas, cierre, precio manual con fecha y moneda, y
      desdoblamientos. **Verificado con datos reales** en
      [`herramientas/prueba-api/posiciones.mjs`](../herramientas/prueba-api/posiciones.mjs):
      **46 comprobaciones, 0 fallos**
  - Se ancla en el caso real de **VEEV** del estado de cuenta: cuatro lotes que dan **34 acciones,
    5 628,443 de coste y 165,5424 de coste medio**. La venta de **11** deja **23 acciones y el coste
    medio **intacto** — que es lo que declara el bróker y lo que hace que la aplicación cuadre con él.
    Si la venta repartiera el coste de otra forma, la media cambiaría y el descuadre aparecería meses
    después, sin saber de dónde viene
  - Y en el **5:1 de MGK**: la cantidad se multiplica por cinco, el coste por unidad se divide, y el
    coste total **no cambia**. Un desdoblamiento no hace a nadie más rico
  - **Una posición por instrumento y cartera**: dar de alta la misma dos veces añade compras a la que
    hay, en vez de crear dos que se sumarían mal
  - El **precio manual lleva fecha y moneda**, y un precio de **cero se rechaza**: un cero no es un
    precio, es un dato que falta disfrazado, y aceptarlo haría que la posición valiera cero y el
    rendimiento saliera como una pérdida total
  - **El aislamiento también aquí**: otra persona recibe **404** al leer, al listar, al vender y al
    cerrar una posición ajena, y la posición queda **sin tocar**. Comprobado
- [x] **Un fallo de EF Core que costó encontrar, y que conviene tener escrito**: EF, al encontrar una
      entidad con la clave **ya puesta**, da por hecho que **existe** en la base de datos y genera un
      `UPDATE` en lugar de un `INSERT`. El dominio asigna el identificador al crear el lote —que es lo
      correcto, una entidad tiene identidad desde que nace— y eso deja a EF con la suposición
      equivocada. Se veía **solo al añadir una compra a una posición que ya existía**: el `UPDATE`
      afectaba a cero filas y saltaba un error de concurrencia que no tenía nada que ver. Los lotes del
      alta inicial sí se guardaban, porque entonces el padre entero era nuevo. Se arregla declarando el
      lote como nuevo explícitamente
- [x] **Búsqueda de instrumentos por nombre** y **guardado separado del código del bróker y de la
      equivalencia con la fuente**, **verificado** con
      [`herramientas/prueba-api/instrumentos.mjs`](../herramientas/prueba-api/instrumentos.mjs):
      **26 comprobaciones, 0 fallos**
  - **Dos búsquedas distintas, y se separan a propósito**: `GET /api/instruments?q=` busca **en el
    catálogo** de la casa (instantánea, no depende de nadie) y `GET /api/instruments/lookup?q=` busca
    **fuera**, en la fuente de mercado. Lo que el usuario quiere saber es distinto: «¿ya tengo esto?»
    frente a «¿cómo se llama esto ahí fuera?»
  - **La bolsa se traduce a código MIC.** La fuente devuelve vocabulario suyo (`NMS`, `NYQ`, `TOR`) y el
    instrumento se identifica por MIC. Comprobado: `NMS` → **`XNAS`**. Copiar el código de la fuente
    metería dos vocabularios en la misma columna, y el mismo valor acabaría con dos instrumentos
  - Cuando la bolsa no se reconoce, el MIC queda **nulo a propósito**: mejor que la pantalla pregunte a
    que se invente un código que no significa nada
  - **Dar de alta dos veces el mismo instrumento devuelve el mismo**, no crea otro. Si creara dos, el
    mismo valor tendría dos precios distintos y la cartera sumaría mal. Es lo que pasa cuando dos
    personas de la casa compran el mismo valor
  - **El catálogo es compartido**: cualquiera puede **añadir** —hace falta para dar de alta la posición
    que acaba de comprar— pero **solo el administrador puede modificar**, porque el cambio lo notan
    todas las carteras a la vez. Comprobado: 403
  - **El ISIN se valida con su dígito de control**: `US5949181044` se rechaza porque el correcto es
    `...045`. La longitud y los caracteres son válidos, así que solo el dígito lo distingue de una errata
  - Una búsqueda **sin resultados no es un error**: devuelve 200 con lista vacía. Tratarla como fallo
    obligaría a la pantalla a distinguir dos casos que para el usuario son el mismo
- [x] **Un límite real de EF Core, encontrado al implementarlo**: con **conversores de valor**, EF **no
      traduce** el acceso a los miembros (`i.Symbol.Value` revienta en ejecución, no al compilar) ni
      sirve `EF.Property`, porque el parámetro acaba pasando por el conversor. Por eso la búsqueda del
      catálogo filtra **en memoria**: para un catálogo familiar son unos cientos de filas. Queda escrito
      en el código que **si pasa de unos miles** hay que añadir una columna de texto del símbolo, para
      que no haya que descubrirlo midiendo
- [x] **Cliente HTTP de pruebas compartido** ([`herramientas/prueba-api/cliente.mjs`](../herramientas/prueba-api/cliente.mjs)):
      sesión con cookies, testigo CSRF y recuento de comprobaciones. Copiado en cada fichero, el día que
      cambie algo habría que acordarse de cambiarlo en todos
- [x] **La vista de portafolio, calculada en el backend**, verificada en
      [`herramientas/prueba-api/vista-cartera.mjs`](../herramientas/prueba-api/vista-cartera.mjs):
      **25 comprobaciones, 0 fallos**
  - **El cálculo vive en el dominio**, no en el lector: este solo junta la cartera, sus posiciones, un
    precio para cada instrumento y los tipos de cambio, y luego traduce el resultado. La aritmética está
    en `PortfolioValuation`, probada aparte y sin depender de dónde vengan los datos
  - **Se calcula en el servidor y no en la pantalla**: los números tienen que salir iguales en el móvil,
    en el ordenador y en un informe, y eso solo se consigue si hay un único sitio que sume
  - **La invariante del ADR-011, comprobada en marcha**: la rentabilidad en dólares y en dólares
    canadienses es la **misma** (34,4861 %), porque el tipo de cambio se cancela. Si no coincidieran,
    significaría que la conversión está mezclando cosas
  - **El mismo tipo de cambio para el valor y para el coste**: comprobado que el cociente coincide. Si
    cada uno usara uno distinto, la ganancia saldría de la nada
  - **Un instrumento en la moneda base no se convierte**: su valor de mercado y el de la moneda base son
    el mismo número, no el resultado de multiplicar por uno
  - **Los pesos suman 1**, que es lo que permite decir «esta posición es el 92 % de tu cartera»
  - **El precio se busca por orden de preferencia**: el puesto **a mano** manda sobre todo lo demás,
    después la última cotización, y después el último cierre. Se devuelve **de dónde salió** cada precio,
    porque no es lo mismo uno que puso una persona que uno de mercado
- [x] **Un fallo grave encontrado al verificar, y del tipo que engaña**: `isComplete` decía **verdadero**
      con una posición sin valorar. La bandera que avisa de que los totales son **parciales** estaba
      mintiendo, y un total parcial presentado como total es una cifra falsa. La causa: al dominio solo se
      le pasan las posiciones valorables —las que no se pueden convertir hay que dejarlas fuera o
      revienta—, así que el dominio no podía saber cuántas se quedaron por el camino, y su propio
      `IsComplete` era cierto por construcción
- [x] **Tipos de cambio, con el puerto y un adaptador básico** (Frankfurter, los del Banco Central
      Europeo: libre, sin registro y sin cuota). Se adelanta un punto de la Fase 5 **a propósito**:
      `CurrencyConverter` nunca inventa un uno a uno, así que sin tipos de cambio la vista no puede sumar
      dólares y dólares canadienses, y una vista de cartera que no da el total no es una vista. **Lo que
      falta de la Fase 5 sigue faltando**: caché por frecuencia, reintentos, interruptor de circuito y
      cadena de respaldo. Hoy se pide al servicio en cada consulta
  - La fecha del tipo de cambio es **la que declara la fuente**, no la de hoy: no se publican los fines
    de semana ni los festivos, y decir que es de hoy cuando es del viernes sería mentir sobre la frescura
    del dato
- [x] **Endpoints agrupados por área, con validación y OpenAPI.** El documento se genera y ahora se
      puede **leer**: las **32 operaciones tienen resumen** (antes, cero), nombres de operación estables
      (`auth.login`, `positions.sell`…) y tags por área (`System`, `Authentication`, `Portfolios`,
      `Instruments`, `Positions`, `Administration`)
  - La **validación es coherente**: devuelve **códigos, no textos** ([ADR-013](decisiones/ADR-013-interfaz-multi-idioma.md)),
    y el mismo código distingue 404 («no es tuya o no existe») de 400 («los datos están mal»), que solo
    uno de los dos se arregla corrigiendo lo que se mandó
  - Un total sin precio o sin tipo de cambio se declara **parcial**, nunca se presenta como si fuera el
    total. Y una búsqueda sin resultados devuelve **200 con lista vacía**, porque «no he encontrado nada»
    es una respuesta legítima, no un fallo

### Fase 5 — Precios y divisas 🔴 *(el corazón del producto)*

- [x] **Puertos `IPriceProvider` / `IFxRateProvider`** — y **en `Application`, no en `Domain`**,
      corregido sobre lo previsto a propósito. El dominio consume el **resultado** (un `Money` con su
      fecha, un `FxRate`), no al proveedor: quién trae el dato es un detalle de infraestructura, y los
      detalles de infraestructura no entran en el dominio. Se deja escrito para no volver a dudarlo
- [x] **Adaptador de la fuente de mercado** (`chart` + `events=div,splits`), con `ProviderKey` por
      instrumento. **Verificado contra la fuente real** en
      [`herramientas/prueba-api/precios.mjs`](../herramientas/prueba-api/precios.mjs):
      **10 comprobaciones, 0 fallos**
  - Lee `close` y **nunca `adjclose`**: el cierre ajustado ya descuenta splits y dividendos, y usarlo
    para valorar una posición que ya aplicó el split sería contar el efecto dos veces
  - Lee el **último cierre no nulo**: los fondos no publican todos los días y en un festivo la casilla
    viene vacía. Comprobado con un fondo real (`0P0001K2XD.TO` → 8,84 CAD) y con la detección de un
    dividendo real en él
  - Un símbolo que no existe devuelve **404**, nunca un precio inventado
- [x] **Adaptador de tipos de cambio (Frankfurter)** — adelantado en la Fase 4 porque la vista de cartera
      lo necesitaba para existir. Sigue faltando su caché, reintentos y respaldo
- [x] **`CompositePriceProvider` que enruta por `ValuationMode`**. Hoy solo hay una fuente detrás, así
      que lo que de verdad decide es la **excepción**: un instrumento de **precio manual no consulta a
      nadie**. Devolver el precio de mercado sería peor que no devolver nada, porque sobrescribiría una
      decisión de la persona con un dato ajeno y sin que se enterara
  - `Market` y `Nav` los sirve la misma fuente: la diferencia entre una acción y un fondo no está en
    dónde se pregunta, sino en **qué se lee** de la respuesta, y de eso se encarga el adaptador con la
    frecuencia de cotización
  - Existe aunque hoy solo haya una fuente, y no es ceremonia: es el punto por el que entrará la **cadena
    de respaldo**. Sin un sitio único donde se decide de dónde sale el precio, esa cadena acabaría
    repartida por todos los sitios que piden precios
- [x] **Caché con TTL por frecuencia**: 15 min intradía, 12 h cierre diario, 24 h valor liquidativo y
      tipos de cambio. **Probada con un reloj inyectable**: 20 pruebas, 0 fallos
  - **La duración depende de cada cuánto cambia el dato**, y ahí está todo: un precio intradía caduca en
    minutos y un valor liquidativo aguanta el día. Con una sola duración, o se pregunta de más a la
    fuente (gastando cuota) o se sirve un precio viejo como si fuera de ahora
  - **La decisión de frescura usa un reloj inyectable, no el del sistema.** No es un capricho: si la
    caducidad la decidiera la caché por su cuenta, probar que un precio caduca a los quince minutos
    exigiría esperar quince minutos de verdad, y esa prueba no se escribiría nunca
  - **Un fallo no se guarda.** Cachear el hueco dejaría el precio en blanco toda la ventana aunque la
    fuente se recuperara al minuto siguiente
  - Los tipos de cambio se guardan **par a par**: si aparece una moneda nueva, se pide solo esa en vez de
    tirar lo que ya se sabía
  - La caché va **por fuera** del enrutado y de la fuente, para que no se ejecuten siquiera cuando el
    precio ya se conoce. Al revés, cada consulta preguntaría igualmente y no ahorraría nada
  - *Nota*: el cierre diario dura 12 h como aproximación a «hasta el próximo cierre». La duración exacta
    la dará el calendario de mercado, que todavía no está: mientras tanto se prefiere preguntar una vez
    de más a servir un cierre viejo como si fuera el de hoy
- [x] **Proyecto de pruebas de infraestructura** (`tests/PortaFolioAcciones.Infrastructure.Tests`), añadido
      al script `probar.cmd` y al README. La caché y el enrutado son reglas con caducidad y contadores:
      probarlas por HTTP dependería de esperas y de tiempos, y esas pruebas acaban desactivándose
  - Las clases de infraestructura siguen siendo `internal`; las pruebas las ven con `InternalsVisibleTo`.
    Hacerlas públicas solo para probarlas abriría la puerta a usarlas desde donde no deben
- [x] **Un fallo encontrado en el propio doble de prueba**: devolvía **todas** las monedas que tenía,
      pidieran lo que pidieran, cuando el puerto real devuelve solo las solicitadas. Un doble que no
      respeta el contrato **esconde el fallo que la prueba busca**: una consulta por una moneda parecía
      haber traído dos
- [x] **Cadena de respaldo**: fuentes en orden y, si ninguna responde, el **último precio conocido marcado
      como tal**. **8 pruebas nuevas**, 0 fallos
  - El último escalón es el que aporta valor real hoy, y existe porque las alternativas son peores:
    **dejar la posición sin valorar rompe el total**, y **poner un cero es mentir**. Un precio viejo,
    dicho como viejo, al menos permite seguir leyendo la cartera
  - **Una fuente que devuelve un respaldo NO cuenta como éxito.** Sin eso, una fuente que arrastra su
    propio último precio taparía a la siguiente de la cadena, y nadie se enteraría de que la primera
    lleva caída
  - **Un respaldo no se cachea**: guardarlo prolongaría la avería. Si la fuente se recupera al minuto
    siguiente, el último precio conocido seguiría sirviéndose hasta que caducara la entrada, y en un
    cierre diario son doce horas
  - **Nunca inventa**: si no hay fuente ni precio guardado, devuelve nulo y quien llama decide. Un hueco
    silencioso sería peor que un hueco
  - Lo que **queda por hacer**: la segunda fuente viva. Hoy solo hay una (la documentación recomienda
    *«fuente gratuita de EE. UU. + Yahoo para Canadá»*, [05](05-precios-y-divisas.md)), y el mecanismo ya
    está listo: se añade a la lista y nada más cambia
- [x] **Resiliencia**: reintentos con espera creciente, interruptor de circuito y cuota diaria. **10 pruebas
      propias**
  - Va en un **manejador HTTP** y no dentro de cada adaptador, porque **es una decisión sobre la red y no
    sobre los precios**. Dentro habría que escribirla tres veces —cotizaciones, búsqueda y tipos de cambio—
    y la tercera quedaría distinta. Se pone con una línea, `ConResiliencia("fuente")`, para que añadir un
    cliente nuevo no pueda olvidarla
  - **Solo se reintenta lo que puede salir bien a la segunda.** Un 404 porque el símbolo no existe no va a
    existir al tercer intento: reintentarlo es gastar cuota para recibir lo mismo
  - **La espera se dobla en cada intento.** Esperar siempre lo mismo sirve de poco: si la fuente está
    saturada, insistir cada medio segundo la satura más
  - **El interruptor tiene tres estados, no dos**, y ahí está todo: cerrado, abierto y **medio abierto**.
    Pasado el tiempo de espera deja pasar **una sola** llamada de prueba; si sale bien se cierra, y si falla
    se vuelve a abrir. Sin esa fase no tendría forma de enterarse de que la fuente se ha recuperado y se
    quedaría abierto para siempre
  - **Y un fallo real de mi primera versión, que encontró la prueba**: la bandera de «deja pasar una prueba»
    se reiniciaba en **cada** fallo, así que una vez abierto el interruptor dejaba pasar una prueba en cada
    intento — **no cortaba nada**. La prueba que lo cazó esperaba 9 llamadas y contó 12
  - **La cuota se corta antes de agotarla.** Pasarse no da un error claro: la fuente empieza a rechazar, y
    ese día no hay precios para nadie. Es mejor dejar de pedir y decirlo
  - **La guarda es por fuente**, no una sola para todas: si fuera compartida, que Yahoo estuviera caído
    dejaría de pedir también los tipos de cambio, que son otro servicio. Un fallo se convertiría en dos
  - **La guarda sobrevive a la petición**, y eso obligó a sacarla del manejador: el marco recicla los
    manejadores de HTTP cada dos minutos, así que un interruptor que viviera dentro se perdería al reciclarlo
  - **Se respeta el `Retry-After`** de la fuente cuando lo manda: es ella la que sabe cuándo podrá atender
- [x] **Calendario de mercado por bolsa** (horarios y festivos). **15 pruebas**, incluidos **varios años**
      y el **cambio de hora**
  - **Los horarios son locales y con su huso, no en UTC.** El horario de Nueva York es 9:30–16:00 **de
    Nueva York**, y esa ciudad cambia de hora dos veces al año. Guardarlo en UTC daría una hora de error la
    mitad del año — y está probado: el cierre de enero cae a las 21:00 UTC y el de julio a las 20:00
  - **Los husos se buscan por su nombre IANA** (`America/New_York`), no por el de Windows: los de Windows no
    existen en el contenedor Linux. Comprobado que se resuelven en este equipo
  - **Los festivos se calculan, no se listan.** Una lista hay que rehacerla cada año, y el día que se
    olvide la aplicación dirá que el mercado está abierto el día de Acción de Gracias. Las reglas —«el
    tercer lunes de enero»— no caducan. Incluye el **Viernes Santo** por el algoritmo gregoriano
  - Cada país tiene **sus** festivos, y se distinguen: el segundo lunes de octubre es fiesta en Canadá y
    día de bolsa en Estados Unidos, y Acción de Gracias es al revés
  - **Una bolsa desconocida no se inventa**: se supone que **sí** se negocia. Suponer lo contrario dejaría
    los instrumentos de esa bolsa **sin actualizar nunca**, y el fallo sería silencioso
  - **Lo que no cubre, y queda dicho**: los cierres por luto nacional o por una tormenta, que se anuncian
    con poco tiempo. Cuando ocurren, el precio que se recoge es el del día anterior y la aplicación lo dice,
    porque lleva su fecha
- [x] **Reglas de `Nav` / `close` / `adjclose`, con pruebas propias del adaptador.** **9 pruebas nuevas** en
      [`YahooChartPriceProviderTests.cs`](../tests/PortaFolioAcciones.Infrastructure.Tests/YahooChartPriceProviderTests.cs)
  - Hasta ahora solo las cubría la prueba **contra la fuente real**, y eso tiene un problema: la fuente
    cambia cada día, y una prueba que depende de ella **falla sola un martes cualquiera**. Las nuevas
    preparan la respuesta y comprueban el análisis
  - **`adjclose` no se lee ni por accidente**, y no por disciplina: el contrato de la respuesta **ni
    siquiera declara ese campo**, así que no hay forma de leerlo aunque alguien se confunda. La prueba lo
    demuestra con dos valores distintos —el ajustado vale 1 y el cierre vale 12,75—, para que el fallo, si
    vuelve, salga como un número y no como una opinión
  - **Se lee el último cierre no nulo**, y la prueba incluye una casilla vacía al final: es lo que pasa en un
    festivo y en los fondos que no publican todos los días
  - **La fecha es la del cierre que se usa**, no la de hoy: la prueba lo comprueba con el segundo de tres
    cierres. Poner la de hoy fecharía un precio en un día en el que no se negoció
  - **Un instrumento sin clave de fuente no llama a nadie**, y se comprueba contando llamadas
  - **Los desdoblamientos hay que pedirlos**, y se comprueba que la petición lleve `events=div,splits`: si no
    se piden, la fuente no los manda y no hay nada que detectar
- [x] **Calendario de mercado por bolsa** (ver arriba, junto a la resiliencia)
- [x] **Detección de splits y aviso al usuario**, con las **dos salidas**. **16 comprobaciones** en
      [`herramientas/prueba-web/desdoblamientos.mjs`](../herramientas/prueba-web/desdoblamientos.mjs)
  - **Un desdoblamiento sin aplicar da una cifra falsa, y de las que asustan**: con un 5:1 la cantidad se
    queda como estaba y el precio se divide, así que la cartera se lee como si hubiera perdido el 80 % de
    un día para otro. Por eso el aviso va **antes de los totales**: lo que anuncia es que los números de
    debajo pueden estar mal
  - **Los splits se guardan en el instrumento y se acumulan.** Es la decisión que hace que no se pierdan:
    la fuente solo declara los eventos del rango que se le pide, que es un mes, así que **un
    desdoblamiento que no se guarde cuando sale no hay forma de recuperarlo**. El job los captura a diario
  - **Y hay dos salidas, no una.** Quien dio de alta sus posiciones copiando un estado de cuenta
    **posterior** al desdoblamiento ya tiene las cifras buenas, y aplicarlo entonces las multiplicaría por
    segunda vez. Sin la segunda opción —«ya está reflejado», que anota sin tocar nada— esa persona solo
    podría elegir entre **estropear sus datos o convivir con un aviso que no puede quitar**. Y un aviso que
    no se puede resolver se aprende a ignorar, y entonces deja de servir el día que importa
  - Solo se avisa a quien **tenía títulos ese día**: si se compró después, el desdoblamiento no le toca y
    avisar sería alarmar sin motivo
  - Hacía falta **registrar los splits aplicados**, y no se podía deducir de los datos: aplicar un 5:1 deja
    la cantidad multiplicada y el coste dividido, pero **nada distingue ese estado del de no haberlo
    aplicado**. Sin ese registro, la aplicación tendría que avisar siempre —o no avisar nunca, que es peor
  - **El instrumento ahora dice qué desdoblamientos conoce**, para que un salto del precio se pueda
    explicar sin adivinar
- [x] **Y dos fallos de procedimiento que aparecieron al aplicar la migración, y que valen más que la
      migración misma**
  - **Los procedimientos documentados de actualización y arranque estaban rotos**: `actualizar.cmd` y
    `arrancar-api.cmd` usaban `if not "%%A:~0,1%"=="#"` para saltar los comentarios del `.env`, y **eso no
    es sintaxis válida de cmd**. Fallaban con «The syntax of the command is incorrect» **antes de ejecutar
    nada**. La forma correcta es `eol=#` en las opciones del `for`. Es decir: **el procedimiento que la
    documentación dice que se use no llegaba ni a empezar**
  - **Y todos los `.cmd` tenían saltos de línea LF.** cmd los tolera en scripts simples, pero **rompe los
    bloques `for ( ... )` de varias líneas**, que es justo lo que tienen esos dos. Se han pasado los cinco
    a CRLF, y queda escrito por qué
  - **`actualizar.cmd` tampoco redirigía la carpeta temporal**, así que la migración fallaba con
    `UnauthorizedAccessException` —y fallaba **después** de haber parado la API, que es el peor momento—.
    El README ya lo decía; el script no lo hacía. Ahora sí
  - **La migración se generó con `defaultValue: ""`**, y **una cadena vacía no es JSON válido**: PostgreSQL
    habría rechazado el `ALTER TABLE` en cuanto hubiera una fila que rellenar, y la tabla tiene filas. Se
    corrigió a `[]` a mano, con el motivo escrito en la propia migración para que no se «arregle» sola la
    próxima vez
- [x] **Job diario** de precios y FX tras el cierre de cada mercado. **Verificado contra las fuentes
      reales**: 43 instrumentos, 4 cierres guardados de Yahoo, 86 fotos de cartera
  - **Espera a que cierren TODAS las bolsas donde hay posiciones.** Recoger en cuanto cierra Nueva York
    guardaría la cartera con los precios de allí y sin los de Toronto, y la foto del día sería la de un
    momento en el que media cartera tenía el precio de ayer
  - **Comprueba si la sesión ha terminado, no si el mercado está abierto.** A las siete de la mañana
    tampoco está abierto, y recoger entonces daría los precios de ayer con la fecha de hoy. Por eso se
    pregunta por **la hora del último cierre** y no por el estado
  - **Es idempotente, y está comprobado**: la segunda ejecución del mismo día guarda **0** y dice que los 4
    ya estaban, con las filas idénticas. Eso es lo que permite ejecutarlo al arrancar, tras una caída o dos
    veces seguidas sin gastar cuota ni duplicar nada — y lo que evita tener que guardar «hasta aquí he
    llegado»: **el propio dato dice si está**
  - **El cierre se guarda con la fecha que dice la fuente, no con la de hoy.** Es la decisión que hace que
    el histórico sirva: en un festivo la fuente devuelve el cierre del día anterior, y guardarlo con la
    fecha de hoy pondría un precio donde no hubo negociación
  - **Un instrumento que falla no tumba el día.** Es la diferencia entre perder un dato y perderlo todo
  - **El contexto se construye con un usuario del sistema**, escrito y visible, en vez de hacer que «sin
    petición» signifique «administrador» en todo el programa: eso convertiría cualquier descuido futuro en
    un acceso a los datos de todo el mundo
  - El servicio en segundo plano vive **en la API**, que es quien hospeda; el colector —lo que hay que
    hacer— se queda en infraestructura. Separados, el trabajo se prueba **sin esperar a ninguna hora**
- [x] **Snapshots diarios de portafolio desde el primer día**. 86 tomadas en la primera ejecución real
  - **No se guarda una foto con el total incompleto.** Es la decisión más importante de esta pieza: una foto
    es un registro histórico, y dentro de un año **nadie sabrá que aquel número estaba a medias**. Un hueco
    se ve; un número bajo, no
  - Se valora con **el mismo lector que usa la pantalla**: valorar aquí por otra vía serían dos cuentas
    distintas de lo mismo, y el día que discreparan no habría forma de saber cuál es la buena
  - **Lo que se guarda y lo que no, con su motivo**: se guardan los totales en la moneda base con su moneda
    —que es lo único **irrecuperable**, porque no hay tabla de tipos de cambio— y **no** las cifras por
    posición en la moneda del instrumento, porque esas **sí se pueden reconstruir exactamente** de los
    lotes, que no cambian. Guardar lo reconstruible sería duplicar la verdad
  - **Un fallo de mi lectura, que conviene dejar escrito**: al ver los cierres, dos parecían sospechosos por
    ser redondos (90,000000 y 23,000000). Fui a la fuente y **eran reales**: MKG cerró a 90 exactos por
    casualidad, y los valores liquidativos se publican con dos decimales. La lección no es «desconfía»,
    es que **comprobar una sospecha cuesta un minuto y una acusación falsa cuesta más**
- [x] **Snapshots diarios de portafolio desde el primer día** (ver arriba, junto al job diario)
- [x] **Etiquetado honesto del rendimiento** ([ADR-011](decisiones/ADR-011-coste-al-cambio-de-hoy.md)): la
      pantalla dice que el rendimiento es **el del activo** y que el coste está **al cambio de hoy**, porque
      cambia a diario sin que haya operaciones
  - **Y un fallo que apareció al ir a comprobarlo**: los tres idiomas decían **«en CAD» a fuego**, y la
    moneda base **es una preferencia del perfil**. En cuanto alguien la pusiera en dólares, la etiqueta
    habría mentido — y una etiqueta que miente es peor que no ponerla, porque da confianza. Ahora la moneda
    se pasa como dato
- [x] **Frescura en la interfaz**: «as of» por posición, mercado abierto/cerrado, aviso de obsoleto, y el
      total parcial con aviso cuando falta algún precio. **7 pruebas** de la regla de frescura
  - **Y un hueco que apareció al hacerlo: la fecha del precio solo se rellenaba en los precios puestos a
    mano.** Las posiciones con precio de mercado no decían de cuándo era — que es justo cuando más hace
    falta saberlo. Ahora la fecha se guarda **junto al precio**, porque cada origen la dice de una forma
    distinta y deducirla después es adivinar
  - **«Mercado cerrado» y «hoy no se negocia» no son lo mismo**, y se distinguen: el segundo explica por qué
    el precio no se mueve. Y se resume de las posiciones, no se pregunta aparte: una cartera puede tener
    valores en dos bolsas, y lo que importa es si **alguno** de esos mercados está abierto
  - **El aviso de precio viejo va aparte del estado del mercado**, porque son cosas distintas: el mercado
    puede estar cerrado y el precio ser el de hoy, que es lo normal. Un precio es viejo cuando es anterior
    a la última sesión de su bolsa
  - **Cada frecuencia tiene su margen, y no es un detalle.** Un fondo publica su valor liquidativo con uno
    o dos días de retraso —es como funciona, no un fallo— así que se le dan tres días. Con la misma
    tolerancia para todos, el aviso saldría todos los días en los fondos, **y un aviso que sale cuando no
    toca enseña a ignorarlos**
  - **Ante la duda no se avisa**: un precio sin fecha, o de una bolsa que no se conoce, no se marca como
    viejo. Suponerlo daría avisos permanentes en instrumentos que quizá estén perfectos

### Fase 6 — Frontend Angular 🟡

- [x] **Login y cierre de sesión**, con la sesión, las guardas y el marco de la aplicación. Verificado:
      **18 pruebas** del frontend y el **flujo completo del navegador a través del proxy** (testigo CSRF →
      entrar → perfil → carteras → crear), en
      [`herramientas/prueba-web/flujo.mjs`](../herramientas/prueba-web/flujo.mjs)
  - **La cabecera se pone una sola vez**, en `shared/marco`, que es la pantalla padre de todo lo que no
    sea entrar. Añadir una pantalla nueva no obliga a acordarse de nada, y la pantalla de entrada puede
    ocupar la ventana entera sin cabecera
  - **El testigo CSRF se renueva al entrar y eso está probado.** Es el fallo que ya apareció en el
    servidor: el testigo pedido estando sin sesión deja de valer al entrar, así que si el cliente guardara
    el viejo, **la primera acción después de entrar fallaría** con un error que no tiene nada que ver con
    lo que la persona está haciendo
  - **Dos peticiones a la vez comparten un solo testigo.** Sin eso, abrir dos pantallas a la vez pediría
    dos y el segundo pisaría al primero, de modo que la petición que ya iba en camino llevaría un testigo
    que acaba de dejar de valer
  - **Los errores viajan como códigos, no como textos** (ADR-013): el servicio devuelve `credenciales` o
    `bloqueado`, y el texto lo pone la pantalla en el idioma de la persona. `bloqueado` se distingue de
    `credenciales` a propósito: uno se arregla esperando y el otro recordando la contraseña, y decirle
    «credenciales incorrectas» a quien está bloqueado solo le hace intentarlo más
  - Si la API falla al salir, **la sesión local se cierra igual**: dejar a alguien «dentro» porque el
    servidor no respondió sería lo peor de los dos mundos, porque cree que ha salido y sigue dentro
  - **Cambiar la contraseña** está incluido, con el aviso de que es temporal: la API invalida la sesión al
    cambiarla, así que después se vuelve a la pantalla de entrada. Eso cierra el flujo de la Fase 3
- [x] **Lista de portafolios y creación**
  - Se enseñan **archivadas incluidas**, en su propia sección: archivar es esconder y no borrar, y si no
    aparecieran en ningún sitio, archivar una por error parecería haberla perdido
  - Se dice **cuál hereda la moneda** del perfil y cuál tiene la suya, porque no es lo mismo: si la
    hereda, cambiarla en el perfil cambia también la de esa cartera
- [x] **Tabla de posiciones**: símbolo, nombre, moneda, cantidad, coste, precio, valor, ganancia absoluta y
      %, peso y fecha del precio. **31 pruebas** del frontend y el **flujo del navegador** por el proxy,
      con datos de demostración: valor **33 846,74 CAD**, ganancia **30,93 %** y pesos que suman 100 %
  - **Se enseña de dónde sale el precio**: `manual`, `quote` o `close`. No es lo mismo uno que puso una
    persona que uno de mercado, ni uno de hoy que uno del último cierre, y quien mira una cifra tiene
    derecho a saber de dónde viene
  - **La moneda va como código** (`CAD 1.234,56`) y **no como símbolo**. Es la decisión que más importa de
    esta pantalla: con dólares canadienses y estadounidenses en la misma tabla, **los dos son «$»**, y con
    símbolos dos columnas parecerían la misma moneda — la más cara sería la equivocada
  - **La ganancia se enseña en la moneda base**, que es la única en la que se pueden sumar posiciones de
    monedas distintas
- [x] **Totales del portafolio**: valor, coste y ganancia total con su rentabilidad
  - **Con el etiquetado honesto del ADR-011**: se dice que el coste está **al cambio de hoy** —cambia a
    diario sin que haya habido ninguna operación, y sin el aviso parecería que se ha comprado o vendido
    algo— y que el rendimiento es **el del activo**, no «lo que has ganado en tu moneda»
  - **Un total sin precio se declara parcial**, y el aviso va **delante** del número: un total incompleto
    presentado como total es una cifra falsa, y quien lo lee tiene que enterarse antes de creérselo
- [x] **Ganancia del día**, con **9 pruebas** de la cuenta y **16 comprobaciones** de punta a punta en
      [`herramientas/prueba-web/ganancia-del-dia.mjs`](../herramientas/prueba-web/ganancia-del-dia.mjs)
  - **Estuvo bloqueada hasta que la Fase 5 llenó `Prices`**, y eso es lo que enseña por qué el orden de las
    fases importaba: la ganancia del día es el precio de hoy menos el cierre anterior, y **el cierre
    anterior no existía en ningún sitio**
  - **Sale de la tabla de cierres que ya está guardada**, y por eso el trabajo diario importa más de lo que
    parece: sin dos días de cierres no hay con qué comparar. Se busca el cierre **estrictamente anterior**
    a la fecha del precio: si el de hoy fuera el de ayer —pasa en un festivo—, compararlo consigo mismo
    daría un cambio de cero, que es mentira
  - **Y lo que hace que la cifra sirva de algo: cuando falta el cierre anterior se dice que falta.**
    **Nulo no es cero.** Un cero diría «no ha cambiado nada», y lo que pasa es que no se sabe —una compra
    de hoy no tiene «ayer», y un fondo que publica cada dos días tampoco—. Esa diferencia es la que evita
    que un total incompleto se lea como completo
  - **El porcentaje del total NO se da si algún cambio falta**, porque se calcula sobre el valor de ayer y
    ese valor también estaría mal: un porcentaje sobre una base equivocada es peor que no darlo. El valor
    de ayer **se deduce restando** —hoy menos lo que ha cambiado— en vez de volver a valorar con los
    precios de ayer, que no se tienen todos
  - En la pantalla, la cifra del día tiene **su propia columna** —con ordenación— y **su propia tarjeta**
    en los totales, con el aviso de parcial ahí mismo: quien la lee tiene que enterarse antes de creérsela
  - **Verificado con datos reales**: AAPL pasó de 336,59 a 343,46, y 10 unidades dan **68,692 USD** de
    ganancia del día, convertidos a **97,27 CAD**. Y en la cartera de demostración se ven las dos caras a
    la vez: MGK **+1.797,00 (+6,79 %)**, GIB.A **−46,38**, TDB2624C **−64,00**, y VEEV con un guion porque
    no tiene cierre anterior — con el total del día declarado **parcial** y sin porcentaje
- [x] **Ordenación por columna** en la tabla de posiciones. **8 pruebas propias**, en
      [`web/src/app/features/carteras/orden.ts`](../web/src/app/features/carteras/orden.ts)
  - **Se ordena sobre los números en crudo, nunca sobre el texto ya formateado.** Es la trampa de esta
    pantalla, y por eso el comparador vive en su propio fichero con sus pruebas: `1.234,56` y `9,00`
    comparados como texto dejan el 9 **detrás** del 1.234, porque compara letra a letra. Con cuatro filas
    no se ve; aparece justo cuando la cartera crece, que es cuando nadie lo está mirando
  - Los textos se comparan **con las reglas del idioma**, para que la Ñ caiga después de la N y no al final
    del alfabeto
  - Al pulsar una columna nueva, los números empiezan **de mayor a menor** y los textos de la A a la Z.
    Empezar los nombres de la Z a la A sería no entender para qué se pulsa una columna de nombres
  - Arranca ordenada **por valor de mayor a menor**: lo que más pesa en la cartera, que es lo que se quiere
    ver al abrir. Alfabético por defecto no diría nada de la cartera
  - Las columnas llevan `aria-sort`, y el área que se puede pulsar es la celda entera —en el móvil, acertar
    a una palabra de doce píxeles es incómodo
- [x] **Responsive real**: en pantalla estrecha la tabla **se convierte en fichas**, con el nombre de cada
      columna delante de su valor. Una tabla de ocho columnas en un móvil obliga a desplazarse en
      horizontal para comparar dos cifras, que es justo lo contrario de para lo que sirve una tabla. **El
      móvil es el dispositivo habitual**, así que esto no es un extra
  - La cabecera también se recoge en un botón, y se respeta el **tema del sistema**: quien tiene el móvil
    en oscuro no debería recibir una pantalla blanca al abrir la aplicación
  - Los campos llevan **16 px o más**: por debajo de eso, el móvil hace zoom solo al enfocar y descuadra
    la pantalla justo cuando se está escribiendo
- [x] **Formulario de alta de posición, con buscador por nombre y verificación contra el extracto.**
      Verificado en el **flujo del navegador**: buscar en el catálogo, buscar en la fuente, dar de alta el
      instrumento y crear una posición con dos compras
  - **Dos búsquedas distintas, y no se mezclan**, porque son preguntas distintas: la del **catálogo** de la
    casa —«¿ya tengo esto?»— se hace sola mientras se escribe, con retardo; la de **fuera** —«¿cómo se
    llama esto ahí fuera?»— hay que pedirla a propósito, porque depende de un servicio ajeno
  - **No se da de alta nada sin confirmarlo.** Al elegir un resultado de fuera se enseñan los datos que se
    van a guardar —incluido **el código del bróker**— y se espera. Esa confirmación es el punto donde una
    persona comprueba que el instrumento es el que dice su estado de cuenta, y es **lo único que resuelve
    el caso de los fondos de Fundserv**: su código no existe en la fuente de precios, así que la
    equivalencia hay que emparejarla a mano
  - Los valores que la fuente no da —cómo se valora, cada cuánto cambia— se rellenan con lo más probable y
    **se enseñan para corregirlos**. Se enseñan, no se deciden en silencio
  - Una compra por línea, **con la exactitud de la fecha**: los extractos no siempre dan la fecha de cada
    compra, y decir que es aproximada es mejor que fingir que se sabe
  - Se acepta **la coma y el punto** como separador decimal: quien escribe en español teclea `165,54` y
    quien escribe en inglés `165.54`, y rechazar una de las dos sería molestar por una convención que la
    persona no eligió
- [x] **Un fallo de diseño que encontró la verificación, y de los que hacen daño**: una posición **sin
      precio no aparecía en la tabla**. La API la dejaba fuera del cálculo —correcto, no se puede valorar—
      pero **no la devolvía en ningún sitio con sus datos**, así que la pantalla solo podía nombrarla en el
      aviso de totales parciales. El efecto para quien la usa: **das de alta una posición y parece que no
      se ha guardado**
  - Ahora la API devuelve las no valoradas **aparte y con sus datos**, y la tabla las enseña **al final,
    con guiones y diciendo por qué** (`sin-precio` o `sin-tipo-de-cambio`). Van aparte de las valoradas y
    no como campos nulos dentro: así, cuando se enseña una posición valorada, sus importes **siempre**
    tienen sentido y no hay que comprobar si son nulos — ese olvido sería una resta que sale mal
- [x] **Edición de una posición ya creada**: añadir una compra, vender, poner el precio a mano, cerrar y
      reabrir, con las notas. Pantalla propia en `/posiciones/:id`, a la que se llega desde el símbolo en la
      tabla de la cartera
  - **La venta no deja elegir qué compra se vende**: se reparte entre todas, que es lo que hace el método de
    coste medio del bróker y **lo que conserva el coste por unidad**. Elegir el lote daría un coste medio
    distinto del que declara el estado de cuenta
  - **Cerrar no borra**: la posición sale de los totales pero conserva sus compras enteras, y se puede
    reabrir
- [x] **Otro fallo real, encontrado al probar el cierre de punta a punta, y de los que no se ven leyendo el
      código: cerrar una posición NO la quitaba de los totales**
  - `Position.Close` pone la marca y **conserva las compras** —para poder reabrir—, así que la cantidad **no
    baja a cero**. `PositionRepository` filtraba por `!IsClosed`, pero **el lector de la vista no pasa por
    el repositorio**: consulta la base directamente. Y `PortfolioValuation` solo descarta las de cantidad
    cero. Resultado: se cerraba una posición y **seguía contando** en el valor y en el coste
  - Ahora el lector separa las cerradas antes de valorar, y las cuenta aparte. Verificado: el valor pasa de
    **5682,8 a 0** al cerrar y **vuelve a 5682,8** al reabrir
  - **Y por qué no lo cazó ninguna prueba**: las pruebas de infraestructura **solo cubren los proveedores de
    datos de mercado**, no tocan la base. Un fallo en una consulta necesita una prueba contra la base, y no
    la hay. Queda abajo como hueco
- [x] **Pruebas contra la base de datos para los lectores**, con **SQLite** y el esquema hecho a partir del
      modelo. **5 pruebas** en
      [`tests/PortaFolioAcciones.Infrastructure.Tests/PortfolioViewReaderTests.cs`](../tests/PortaFolioAcciones.Infrastructure.Tests/PortfolioViewReaderTests.cs)
  - **Y se comprobó que sirven de algo**: se reintrodujo el fallo a propósito —quitar el filtro de
    cerradas— y la prueba falló con **1550 en vez de 1200**, que son exactamente los 350 de la posición
    cerrada (5 × 70) colándose en el total. Un test que no falla cuando el fallo está no protege de nada
  - **SQLite y no el proveedor «en memoria»**, aunque los dos valdrían para esto: el de en memoria **no es
    una base de datos** —no aplica restricciones ni traduce las consultas a SQL—, así que una consulta que
    PostgreSQL rechazaría la pasaría tan contenta
  - Las migraciones **no** se aplican: son de PostgreSQL (`numeric(28,4)`, tipos propios) y no valen aquí.
    El esquema sale del **modelo**, que es el mismo en los dos sitios. Se anota como límite: una prueba así
    no comprueba que las migraciones estén bien
  - Cubre las cuatro cosas que importan del lector: que la cerrada no cuenta, que la cartera de otra
    persona **no se ve** —ni por acceso directo ni en un listado—, que el administrador **sí la ve**, y que
    una posición sin precio **se devuelve aparte y con sus datos** para poder enseñarla
- [x] **Selector de moneda base por usuario y de idioma**, en la pantalla de **Preferencias**. Verificado:
      se guarda, **se vuelve a leer y sigue puesto**, y `/api/auth/me` lo dice igual
  - **La moneda base se explica en la propia pantalla**: *«es la moneda en la que se suman tus totales. No
    cambia lo que tienes: una acción comprada en dólares sigue siendo la misma, pero su valor se convierte
    a esta moneda para poder sumarlo con las demás»*. Confundir esto lleva a creer que cambia lo que se
    tiene, y no cambia nada de lo comprado
  - **Dos huecos que aparecieron al construirla, y ninguno se veía desde la pantalla**:
    - **El selector de idioma no guardaba nada.** Cambiaba la interfaz y al siguiente inicio de sesión
      volvía al idioma del perfil. Ahora se guarda, y **se aplica primero y se guarda después**: si se
      esperara a la respuesta, cambiar de idioma tardaría lo que tarde la red, y si la red falla no
      cambiaría nada
    - `User.ChangeBaseCurrency` y `User.ChangePreferredLanguage` **existían en el dominio desde la Fase 2
      y no había ningún endpoint que los llamara**. Estaban escritos, probados y sin usar
  - **Los dos códigos van en una sola petición**, porque son la misma pantalla: con dos, un fallo en la
    segunda dejaría las preferencias a medias
  - **Un código mal escrito da 400, no 404**: no es lo mismo. Un 404 haría creer que el perfil se ha
    perdido, que es justo lo contrario de lo que pasa
  - **El perfil se lee por un solo sitio**: `/api/auth/me` consultaba la tabla por su cuenta, y con esta
    pantalla habría quedado **la misma lectura escrita dos veces**
- [x] **Comprobador de idiomas**, en
      [`herramientas/comprobar-idiomas`](../herramientas/comprobar-idiomas/comprobar.mjs): los tres
      ficheros tienen **exactamente las mismas 155 claves**, y caza las repetidas dentro de uno mismo
  - Existe porque **un idioma al que le falta una clave no falla**: Transloco devuelve la propia clave y la
    pantalla enseña `posiciones.precioManual` a la persona. Sin error, sin aviso, y en español —el idioma
    en el que se escribe el código— **no se nota nunca**
  - **Se ganó el sueldo en su primera ejecución**: encontró `cartera.tituloPlural` repetida en francés y en
    inglés, de una edición mía que la había duplicado. En JSON gana la última, así que se habría perdido en
    silencio
- [x] **Pantalla de auditoría** para el administrador, filtrable por persona, fecha y entidad
      ([ADR-014](decisiones/ADR-014-administrador-escritura-con-auditoria.md)). **34 comprobaciones**, 0 fallos
  - La API tenía el libro **escrito y probado desde la Fase 3** y **ningún endpoint para leerlo**. Estaba
    todo: el registro, su inmutabilidad, el valor anterior y el nuevo… y no había forma de mirarlo
  - **La pantalla está construida alrededor de una sola pregunta: «¿qué se ha tocado que no fuera mío?»**
    Por eso el filtro de *solo datos de otras personas* viene **activado de entrada**, y por eso las
    anotaciones ajenas se distinguen con el borde y no con un color de fondo —el fondo se pierde al
    imprimir y en modo oscuro—. Enseñar el libro entero sin jerarquía convierte la revisión en un trabajo
    que nadie hace
  - **El libro no se puede tocar, y se comprueba**: `POST`, `PUT` y `DELETE` sobre `/api/admin/audit`
    devuelven 404. Un registro que se puede modificar no es un registro
  - La lista de personas **sale del propio libro** y son los **dueños**, no los autores. Costó un fallo
    descubrirlo: al incluir también a los autores, elegir a uno de ellos no devolvía nada — un filtro que
    parece roto
- [x] **Aviso permanente de en qué cuenta y de quién** cuando el administrador toca datos ajenos
  - **Y debajo había un fallo peor que un aviso que falta: faltaban los datos.** El administrador veía
    **86 carteras de 17 personas** en la misma lista **sin que la respuesta dijera de quién era ninguna**.
    No es solo confusión —«¿cuáles son las mías?»—: podía **abrir y modificar la cartera de otra persona
    sin saberlo**
  - Ahora la cartera dice de quién es, el listado separa las mías de las de las demás y las agrupa por
    persona, y el aviso aparece **en la pantalla donde se toca** —también al entrar por un enlace directo,
    que es el caso que un aviso puesto solo en el listado no cubre
  - El aviso es **un componente compartido** y no un trozo de plantilla repetido: tiene que decir
    **exactamente lo mismo** en todas las pantallas. Copiado, el día que se matice en una se quedará
    distinto en las demás, y un aviso que dice cosas distintas según dónde se mire deja de ser un aviso
  - **No se puede cerrar.** Un aviso descartable desaparece justo cuando se lleva un rato trabajando, que
    es cuando más falta hace
  - Los avisos van **lo primero, antes del título**. Si fueran después de los números, ya se habría leído
    la cuenta entera creyendo que es propia
- [x] **Un hallazgo del propio trabajo, y de los que dan que pensar: mi script de verificación imprimió un
      nombre real**
  - La base de datos **tiene los datos de verdad** —es la aplicación de una casa, para eso está— y los
    nombres de dentro son reales. La base **no se publica**, así que no es una fuga al repositorio
  - Pero **una salida de consola acaba copiada en un documento**, y por ahí sí se cuela. Ocurrió: la primera
    versión de [`herramientas/prueba-web/administracion.mjs`](../herramientas/prueba-web/administracion.mjs)
    imprimió un nombre al informar de qué carteras eran de otras personas
  - Ahora ese fichero **no imprime ningún nombre de persona ni de cartera**: cuenta, compara por
    identificador y no imprime los valores. Queda escrito en su cabecera, con el porqué
  - **La lección**: el comprobador de datos personales mira los ficheros, y hace bien, porque son lo que se
    publica. Pero el camino real de una fuga es **copiar algo de la aplicación a la documentación**, y eso
    no lo vigila nadie. La regla es no sacar nombres de la aplicación, ni siquiera a la consola
- [x] **Sin lógica financiera en el cliente**: se formatea y se presenta, nunca se calcula. Los totales
      llegan sumados y convertidos de la API; el cliente solo los escribe según el idioma
  - **Los números viajan en crudo** y se formatean al presentar. Y hay tres trampas que quedaron probadas:
    el español **no agrupa los millares de cuatro cifras** (`1234,56` es correcto, y suponer lo contrario
    escribe una prueba que falla sin que nada esté mal); una fecha `2026-08-31` **no puede construirse con
    `new Date('2026-08-31')`**, porque se interpreta como medianoche UTC y en América eso es el día
    anterior; y el porcentaje se escribe con `Intl`, no multiplicando a mano

### Fase 7 — Operación y entrega 🔴

- [x] **Despliegue local funcionando con Docker Compose. Y NO lo estaba.** Caddy respondía en `/` con
      **HTTP 200 y el cuerpo vacío**: la API funcionaba, la comprobación de salud decía que todo estaba
      bien, y **la aplicación no existía**. Solo se veía al abrirla en el navegador — que es justo lo que
      nadie hace cuando las pruebas pasan. **24 comprobaciones** en
      [`herramientas/prueba-web/despliegue.mjs`](../herramientas/prueba-web/despliegue.mjs)
  - El `Caddyfile` lo decía y nadie lo había hecho: *«cuando exista el frontend de Angular, se añadirá aquí
    su servido»*. Ahora **Caddy lleva la aplicación dentro**: se construye con
    [`web/Dockerfile`](../web/Dockerfile) en dos etapas —Node para compilar, Caddy para servir—, así que la
    imagen final no lleva Node ni el código fuente
  - **El respaldo a `index.html` para las rutas profundas** es lo que hace que guardar un enlace o recargar
    en `/carteras/xxx` funcione. Sin él, esa ruta no existe como fichero y daría 404
  - **Pero ese respaldo NO se aplica a `/api`**, y se comprueba: si se aplicara, un error de la API llegaría
    al cliente como una página HTML con un 200, y el cliente intentaría leerla como datos
  - **Las traducciones se sirven sin caché**, y es a propósito: no llevan identificador en el nombre, así
    que cachearlas dejaría los textos viejos después de una actualización
- [x] **Respaldo automático** con retención. [`respaldar.cmd`](../respaldar.cmd) y
      [`herramientas/respaldo/respaldar.ps1`](../herramientas/respaldo/respaldar.ps1)
  - **El volcado se hace con `-f` dentro del contenedor y se saca con `docker cp`**, en vez de redirigir la
    salida: redirigir un flujo binario desde PowerShell lo corrompe **en silencio** —el fichero queda con el
    tamaño correcto y no se puede restaurar—, y eso se descubre el día que hace falta
  - **Se comprueba que el volcado se puede leer antes de darlo por bueno**, con `pg_restore --list`. Un
    respaldo que nunca se ha comprobado no es un respaldo: es un fichero del que se supone que sirve
  - **Formato comprimido y no SQL de texto**: ocupa mucho menos y se puede verificar sin restaurarlo entero
  - **La retención ordena por el nombre y no por la fecha del fichero**: copiar la carpeta a otro sitio
    cambia las fechas del sistema de ficheros, y entonces borraría los que no toca
  - Deja un **registro** en `respaldos\respaldo.log`. Cuando lo ejecuta el Programador de tareas, sin
    registro un respaldo que lleva un mes fallando no se distingue de uno que funciona
  - [`programar-respaldo.cmd`](../programar-respaldo.cmd) lo deja programado a las 03:00 con una orden. **No
    se ha ejecutado**: crear una tarea programada en el equipo de alguien es una decisión suya, y además solo
    funciona si Docker está arrancado a esa hora. Se ejecuta como administrador, una vez, y se quita con
    `schtasks /delete /tn "PortaFolio - respaldo" /f`
- [x] **Restauración probada de verdad.** [`restaurar.cmd`](../restaurar.cmd) y
      [`herramientas/respaldo/restaurar.ps1`](../herramientas/respaldo/restaurar.ps1)
  - **Por omisión NO toca la base buena**: restaura en una base aparte, la de comprobación. Restaurar encima
    de los datos de verdad es una decisión, y por eso hay que pedirlo con `-Real`, que además pide
    confirmación escribiendo `RESTAURAR`
  - **Probado**: se restauró el respaldo más reciente y salió **exactamente lo mismo que hay en la base
    viva** — `Users=109 Portfolio=174 Instruments=67 Positions=125 Lots=260`
  - **La comprobación de lo restaurado va en un fichero SQL y no en la línea de comandos**, y no es un
    capricho: al pasar por `cmd`, las comillas dobles de los nombres de tabla se pierden y PostgreSQL los lee
    en minúsculas, así que la comprobación falla **por el entrecomillado y no porque el respaldo esté mal**.
    Ese error ya había aparecido varias veces en este proyecto, siempre con la misma forma
  - **Y la primera versión de esta comprobación no comprobaba nada**: imprimía «Restauración completada»
    aunque la consulta hubiera fallado. Ahora, si la comprobación falla, el script falla — y si era la base
    de verdad, **deja la API parada a propósito**
  - Al restaurar sobre la base de verdad, **hace antes una copia de lo que hay**: el orden es siempre copia,
    parar, restaurar, arrancar
- [x] **Y un hueco de seguridad que apareció al hacer el respaldo: `.gitignore` no excluía `respaldos/`**
  - Un volcado contiene **la base entera** —los nombres, las cuentas, las posiciones y el libro de
    auditoría— en un solo fichero
  - **Y la comprobación automática de datos personales no lo habría visto**: busca texto, y un volcado está
    **comprimido**, así que los nombres no aparecen como texto y los daría por buenos. Esa línea del
    `.gitignore` es la única protección que hay, y quedó escrita con ese motivo al lado
- [x] **Alta de las cuentas y carga de las posiciones reales** desde `listado.csv`, con una herramienta que
      **lee los ficheros y no lleva los datos dentro**:
      [`herramientas/cargar-datos/cargar.mjs`](../herramientas/cargar-datos/cargar.mjs)
  - **28 posiciones en 7 cuentas de 2 personas**, cargadas. Con `--simulacro` se puede ver el plan sin
    escribir nada
  - **El cargador no lleva ni un dato personal**: si los llevara, el repositorio se publicaría con el
    patrimonio de la familia, que es justo lo que el `.gitignore` y el comprobador evitan. Lee `listado.csv`,
    `lotes.csv` y el mapa de instrumentos, todos fuera del repositorio
  - **Y por pantalla no sale ningún nombre, ni siquiera el nombre de entrada** —que se deriva del real—:
    dice «persona 1», «persona 2». Una consola acaba copiada en un documento. Las claves temporales van a
    `credenciales-iniciales.txt`, que el repositorio ignora
  - **Se entra como cada persona en vez de crear desde el administrador**, porque el alta de cartera crea
    para quien la pide: el administrador puede **leer y modificar** datos ajenos, pero no hay forma de crear
    una cartera a nombre de otro. Entrar con su clave temporal usa el camino normal
  - **La clase de activo y la bolsa se preguntan a la fuente**, no se adivinan. Y cómo se consigue el precio
    —`Nav` para fondos, `Market` para lo demás— se deduce con una regla escrita, que es una decisión y no un
    dato del estado de cuenta
  - **Dos fallos míos, y el segundo es el que enseña algo**: la primera versión del `--simulacro` **escribió
    de verdad** —creó 17 instrumentos, porque solo había protegido las carteras—. Y la primera versión
    **imprimía los nombres de entrada**. Un simulacro que escribe y una herramienta que filtra lo que existe
    para no filtrar son las dos formas de fallar de un programa así
  - **La carga destapó que el alta de cuentas devolvía un 500 cuando faltaba la contraseña**: el nulo llegaba
    hasta Identity y reventaba con una traza. Un campo que falta es una petición incompleta, no un fallo del
    servidor: ahora responde **400**
  - **Y destapó que un administrador no podía saber quién tiene acceso**: no había forma de listar las
    cuentas sin entrar en la base de datos. Ahora hay `GET /api/admin/users`
- [x] **Cargar los lotes reales** desde `lotes.csv`: **15 con fecha exacta y 15 con la fecha del estado**,
      marcada como orientativa
  - Los lotes que no traen fecha usan **la fecha del estado de cuenta**, y van marcados como `Reference` con
    una nota que lo dice: **no es la fecha de compra, es la única que se sabe**. Fingir que es exacta
    estropearía el cálculo de la rentabilidad por tiempo, que es lo único para lo que sirve una fecha
  - **Y un hallazgo que solo se ve al comparar: el estado da el coste unitario redondeado y el total exacto.**
    110 × 115,77 son 12.734,70 y el estado dice 12.734,85. Guardar el unitario desviaba **cada posición unos
    céntimos** y hacía que los totales no cuadraran nunca. Se guarda el total dividido entre las unidades,
    que es el dato que se conserva exacto
  - **Y los lotes y el estado pueden no cuadrar al céntimo entre ellos**, porque salen de dos extracciones
    distintas: ASML sumaba 7.651,93 frente a 7.651,92 del estado, y VEEV 3.807,48 frente a 3.807,47. Manda el
    estado —que es el documento— y **el último lote absorbe la diferencia**, avisando de cuánto era
- [x] **Verificación contra el bróker**: **los totales cuadran con los estados, 27 posiciones y 0 descuadres**.
      [`herramientas/prueba-web/verificacion-broker.mjs`](../herramientas/prueba-web/verificacion-broker.mjs)
  - **Se compara lo que de verdad es comparable, y no lo que no**: las **cantidades y los costes** tienen que
    coincidir **exactos**, porque son hechos históricos y no cambian con el tiempo. Los **valores de mercado
    no** —la aplicación usa el precio de hoy y el estado es del 31 de agosto—, y compararlos daría un fallo
    falso. Se enseñan al lado, como información
  - **Comprueba la aritmética de la aplicación contra sí misma**, aparte del estado: que el valor sea de
    verdad cantidad por precio y que la ganancia sea valor menos coste. Si eso falla, el problema no son los
    datos cargados
  - **Y la prueba encontró los dos céntimos.** Una verificación que solo dice «bien» no vale nada; esta dijo
    exactamente en qué dos posiciones y por cuánto
- [x] **Guía de uso para la familia**, dos páginas: [09-guia-de-uso.md](09-guia-de-uso.md)
  - Cómo se entra —y por qué **no hay «recuperar contraseña»**: se entrega una temporal en mano, porque un
    mensaje se reenvía y un papel se pierde
  - Qué significa cada columna, con la advertencia en cada una de lo que se puede malinterpretar
  - **Los tres avisos que cambian cómo se lee la pantalla**: «total parcial», «desactualizado» y «hoy: solo lo
    que se sabe»
  - **Los dos avisos que piden que hagas algo**, con cuál de las dos salidas elegir en cada caso
  - **Y lo que la aplicación NO hace**, que es tan importante como lo que hace: no lleva la contabilidad de
    compras y ventas, no calcula el coste fiscal, no opera y no da consejos
  - Y **qué hacer cuando algo no cuadra**, en orden: mirar la fecha del precio, mirar si es parcial, comparar
    la cantidad con el estado, y solo entonces preguntar

---

## Después del MVP: la hoja de ruta

**Nada de esto bloquea el producto funcional**, y por eso ya no son casillas: una casilla pendiente dice
«falta para terminar», y esto no falta — es lo siguiente que se puede hacer. Están ordenadas por lo que
aportarían, no por lo que costaría hacerlas.

| # | Qué | Por qué estaría bien | Lo que hay que saber antes |
|---|---|---|---|
| 1 | **Gráfico histórico de evolución** | Es lo que más se echa de menos al mirar una cartera: la cifra de hoy sin la curva de cómo se llegó a ella dice poco | **Los datos ya se están guardando** desde el primer día (una foto diaria por cartera). Cuanto antes se haga la pantalla, más historia habrá; los días que pasan sin ella **no se pierden** |
| 2 | **Desglose del efecto divisa** | Separa lo que ha subido el activo de lo que ha movido el tipo de cambio. Con una cartera en dólares y la base en dólares canadienses, **es la diferencia entre «mi cartera sube» y «el dólar sube»** | Ya está resuelto a medias: el rendimiento local y el de la moneda base se calculan por separado. Falta presentarlo |
| 3 | **Importación CSV del bróker** | La carga inicial ya está hecha; esto es para no tener que repetirla a mano cada vez que se añade una posición | **Es la pieza que más se ha evitado a propósito**: una importación mal hecha estropea los datos en silencio. Habría que reutilizar el cargador, que ya sabe leer los ficheros |
| 4 | **Exportación a CSV / PDF** | Para llevarle algo al contador, o para guardar un resumen del año | Sencillo, y con un destino claro. Hoy no hay forma de sacar los datos de la aplicación |
| 5 | **Asignación por tipo de activo, sector y país** | Responde «cuánto tengo en tecnología» o «cuánto fuera de Canadá», que es la pregunta siguiente a «cuánto tengo» | Necesita un dato que hoy no se pide en ningún sitio: el sector y el país de cada instrumento |
| 6 | **Comparación con un índice de referencia** | Un 8 % no significa nada sin saber qué hizo el mercado | Necesita historia de precios de referencia, que hoy no se recoge |
| 7 | **Intradía con 15 min de retraso** | Ver el precio moverse durante la sesión | **Es el que menos aporta**: la aplicación es para consultar, no para operar, y los precios de cierre bastan para eso. Y multiplica por veinte las llamadas a la fuente, con su cuota |
| 8 | **Segundo factor (TOTP)** | **Sí hace falta si algún día se sale de la red de casa** | Dentro de casa, y por HTTP local, un segundo factor por aplicación no aporta: lo que protege es no exponerla |
| 9 | **Alertas de precio por correo** | Avisar cuando algo cruza un umbral | Necesita correo saliente y, sobre todo, decidir **cuántas alertas aguanta una persona antes de ignorarlas** |

---

## Decisiones abiertas

Cerradas el 2026-10-12: la **validación** (DataAnnotations), los **formatos e idioma** —será multi-idioma
en español, francés e inglés ([ADR-013](decisiones/ADR-013-interfaz-multi-idioma.md))— y la pregunta
**D4** —el administrador **lee y modifica** datos ajenos, y cada cambio queda auditado
([ADR-014](decisiones/ADR-014-administrador-escritura-con-auditoria.md))—.

Queda **una**, y no bloquea el andamiaje:

- [x] **Migraciones de EF Core**: **paso explícito dentro de un comando único** `actualizar.cmd` (copia → parar → migrar → arrancar). La base solo cambia de estructura con una versión nueva; el comando elimina la pregunta del orden
- [x] **Alcance del administrador**: **lee y modifica datos ajenos, y cada cambio queda auditado** ([ADR-014](decisiones/ADR-014-administrador-escritura-con-auditoria.md))
- [x] **Restablecimiento de contraseña**: el **administrador genera una temporal** y la comunica en
      persona, con caducidad y cambio obligatorio al entrar
      ([ADR-006](decisiones/ADR-006-autenticacion-local-identity-cookie.md))

**El diseño queda cerrado: ya no hay ninguna decisión abierta.** Los detalles de las dos últimas quedan
explicados abajo, como registro.

### Ya resuelto: restablecimiento de contraseña

Al no haber servidor de correo ([ADR-006](decisiones/ADR-006-autenticacion-local-identity-cookie.md)),
no se puede enviar un enlace de recuperación. **Resuelto el 2026-10-12:** el administrador genera una
temporal y la comunica en persona. Requisitos fijados en el ADR-006: se muestra **una sola vez**, caduca
en 48 horas, **obliga a cambiarla al entrar**, invalida las sesiones abiertas y **queda auditada** (el
hecho, nunca la contraseña).

Con esto, **el administrador puede técnicamente entrar en cualquier cuenta**. Es coherente con el
[ADR-014](decisiones/ADR-014-administrador-escritura-con-auditoria.md), donde la auditoría es la
contrapartida de ese poder.

### La última decisión, explicada

*(ya resuelta más abajo)*

**Restablecimiento de contraseña.** Al no haber servidor de correo
([ADR-006](decisiones/ADR-006-autenticacion-local-identity-cookie.md)), no se puede enviar un enlace de
recuperación. Si alguien olvida su contraseña, las opciones son:

- **(a)** el **administrador le genera una temporal** y se la da en persona. Es una pantalla y un botón.
  *Recomendado.*
- **(b)** montar un servidor de correo solo para esto: más infraestructura y más cosas que pueden fallar.
- **(c)** no poder recuperarla: si se olvida, se pierde la cuenta.

Con la opción (a), **el administrador puede técnicamente entrar en cualquier cuenta**. Para 2–5 personas
que conviven es normal, y ahora conviene saberlo más que antes: con el
[ADR-014](decisiones/ADR-014-administrador-escritura-con-auditoria.md) ya puede además **modificar** los
datos de cualquiera. **La auditoría es la contrapartida de ese poder.**

### Ya resuelto: migraciones de EF Core

Cuando el programa cambia de versión, la estructura de la base de datos tiene
que cambiar con él (una tabla nueva, una columna nueva). Ese cambio se llama migración, y la pregunta era
quién lo ejecuta:

- **(a)** **la aplicación sola, al arrancar**: cómodo, pero si algo sale mal ya ha tocado la base antes de
  que puedas hacer copia de seguridad, y no hay quien lo revise.
- **(b)** **un paso explícito**, después de la copia: más seguro, pero hay que acordarse.

**Resuelto el 2026-10-12:** se adopta **(b) con el añadido** — la aplicación **se niega a arrancar** si la
base está desactualizada. En la práctica se envuelve en un **comando único** `actualizar.cmd`
(copia → parar → migrar → arrancar), así que no hay orden que recordar. Fijado en el
[baseline](07-baseline-tecnologico.md).

---

## Avisos que no conviene olvidar

- **Licencias**: antes de añadir cualquier paquete, comprobar su licencia. Entre 2025 y 2026 pasaron a
  modelos comerciales o duales MediatR, AutoMapper y FluentAssertions — tres librerías que aparecen en
  casi todos los tutoriales de .NET.
- **Versiones**: no usar .NET 8 ni .NET 9 (fin de soporte el 2026-11-10). Angular obliga a una
  actualización mayor **aproximadamente cada año**, de forma indefinida.
- **El riesgo del ADR-004 sigue vivo**: la fuente sin contrato cubrirá la mayor parte de la cartera
  porque la moneda base es CAD. Mitigado por diseño (puerto de precios + valoración manual), y el día
  que falle la salida natural son 19,99 USD/mes de EODHD, no una tarde de programación.
- **El archivo `instrumentos_old.csv`** que hay en la carpeta de la herramienta no lo creé yo. Si es
  una copia tuya de seguridad, conviene borrarlo o moverlo fuera para no confundirlo con la plantilla.
- **Cuidado con las cantidades «previas a la liquidación».** El estado de agosto mostraba **VEEV 34**
  cuando la posición económica era **23**, porque 11 acciones vendidas el 31 de agosto aún no se habían
  liquidado. Hay que usar la **cantidad efectiva** (la columna «segregado»), no la de la tabla de
  posiciones. Cargar 34 habría añadido 11 acciones fantasma.
- **Los splits no son teóricos en esta cartera**: dos en un año (NFLX 10:1 y MGK 5:1). Sin detectarlos,
  la cantidad y el precio dejan de ser coherentes y las cifras se van sin que nada falle.
- **Docker no está instalado en este equipo** (comprobado el 2026-10-12: `dotnet 10.0.301`, `node 24.16.0`,
  `npm 11.13.0` y `git 2.54.0` sí están; `docker` no existe). Afecta al `docker-compose.yml` de la Fase 1 y
  a PostgreSQL. Hay que decidir entre **instalar Docker Desktop** o **PostgreSQL nativo en Windows**.
  Ni la Fase 1 (el andamiaje) ni la Fase 2 (el dominio) dependen de ello.
- **El libro de auditoría no puede tener borrado en cascada.** Si se borra una posición y el registro de
  auditoría se va con ella, se pierde justo la prueba de que existió. El libro guarda tipo e identificador
  como referencia lógica, sin clave foránea ([ADR-014](decisiones/ADR-014-administrador-escritura-con-auditoria.md)).
- **La auditoría no puede depender de que alguien se acuerde de escribirla.** Va en un interceptor de
  EF Core, dentro del mismo `SaveChanges` que la modificación. Una llamada manual en cada caso de uso es
  una auditoría que el día que se olvide no dejará rastro de que se olvidó
  ([ADR-014](decisiones/ADR-014-administrador-escritura-con-auditoria.md)).
