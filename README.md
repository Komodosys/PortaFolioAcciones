# PortaFolioAcciones

Seguimiento de cartera para uso **personal y familiar** (2–5 personas, cada una con su cartera
aislada). Solo seguimiento: posiciones, precios y rendimiento. Sin registro de operaciones ni
analítica avanzada.

- **Frontend**: Angular
- **Backend**: .NET 10 (C#), ASP.NET Core
- **Base de datos**: PostgreSQL
- **Arquitectura**: capas limpias, con las dependencias apuntando **al dominio**

Toda la documentación de análisis y las decisiones están en [`DOCUMENTACION/`](DOCUMENTACION/README.md).
El estado del trabajo y lo que falta, en [`DOCUMENTACION/TODO.md`](DOCUMENTACION/TODO.md).

## Estructura

```
src/
  PortaFolioAcciones.Domain/          Entidades, objetos de valor y reglas. SIN dependencias.
  PortaFolioAcciones.Application/     Casos de uso y puertos. Depende solo del dominio.
  PortaFolioAcciones.Infrastructure/  Precios, divisas, EF Core, correo. Implementa los puertos.
  PortaFolioAcciones.Api/             Minimal APIs, autenticación, composición.
tests/
  PortaFolioAcciones.Domain.Tests/         Pruebas del dominio.
  PortaFolioAcciones.Architecture.Tests/   Vigila que la regla de dependencias se cumpla.
  PortaFolioAcciones.Infrastructure.Tests/ Pruebas de la caché de precios y de tipos de cambio.
web/                                  Aplicación Angular.
herramientas/                         Utilidades de análisis (extracción de estados de cuenta, cobertura).
```

Las versiones de los paquetes NuGet se declaran **una sola vez** en
[`Directory.Packages.props`](Directory.Packages.props): los proyectos dicen qué necesitan y no con qué
versión. Se cambia en un único sitio, y así dos proyectos no pueden acabar pidiendo versiones distintas
de lo mismo.

## Cómo compilar y probar

```cmd
dotnet build
probar.cmd
```

`probar.cmd` compila y ejecuta las pruebas de todos los proyectos de tests.

**Las suites de punta a punta escriben en la base de datos de verdad.** No es un descuido que se pueda
arreglar con una línea: comprueban la aplicación entera a través de su API, y para eso crean cuentas y
carteras. La consecuencia práctica es que **hay que ejecutarlas antes de cargar los datos reales, o vaciar la
base después** — si no, aparecen carteras de prueba mezcladas con las de la casa.

La forma limpia de dejarla como recién entregada es vaciarla y volver a cargar:

```cmd
docker compose exec -T postgres psql -U portafolio -d postgres -c "DROP DATABASE portafolio WITH (FORCE)"
docker compose exec -T postgres psql -U portafolio -d postgres -c "CREATE DATABASE portafolio"
actualizar.cmd
node herramientas\cargar-datos\cargar.mjs
```

Hacer esto con datos de verdad dentro **destruye esos datos**: antes hay que ejecutar `respaldar.cmd`, y
saber que `restaurar.cmd -Real` los devuelve.

## Cómo levantar la aplicación

```cmd
docker compose up -d --build      construye y levanta todo
docker compose ps                 estado y salud de los tres servicios
docker compose logs -f api        seguir los registros
docker compose down               parar (los datos NO se pierden)
```

Luego se abre **http://localhost:8080**. La comprobación de salud está en
**http://localhost:8080/salud** y dice si la API alcanza la base de datos de verdad.

### Entrar por primera vez

La primera cuenta de administrador **se crea sola** al arrancar, y solo la primera vez. Se entra con:

- **Usuario:** el valor de `ADMIN_USERNAME` en el `.env` (por defecto, `admin`)
- **Contraseña:** el valor de `ADMIN_PASSWORD` en el `.env`

Si `ADMIN_PASSWORD` está vacío, el arranque **genera una contraseña** y la enseña **una sola vez** en el
registro, para no obligar a guardarla en un fichero:

```cmd
docker compose logs api | findstr CONTRASENA
```

A partir de ahí, esa sección del `.env` no se vuelve a mirar: **las demás cuentas las crea el
administrador** desde la propia aplicación. No hay registro público, porque esta aplicación es de una casa
y no de usuarios por captar.

> Si al entrar sale «Usuario o contraseña incorrectos» y estás seguro de la contraseña, **mira el nombre de
> usuario**: es fácil escribir el nombre de la variable (`ADMIN_PASSWORD`) en lugar del usuario. Pasó de
> verdad, y por eso ahora los dos están en el `.env` y no solo la contraseña.

### Frontend en desarrollo

```cmd
cd web
npm start        http://localhost:4200, con proxy de /api hacia la API
```

Detalles en [`web/README.md`](web/README.md).

Para actualizar a una versión nueva, nunca se migra a mano:

```cmd
actualizar.cmd
```

Hace los cuatro pasos en orden —**copia de seguridad, parar, migrar, arrancar**— y si la migración
falla no arranca nada. La aplicación **se niega a arrancar** si la base de datos tiene migraciones
pendientes: es un cinturón, para que nadie acabe con un esquema viejo por un olvido.

### Servicios

| Servicio | Imagen | Publicado al exterior |
|---|---|---|
| `caddy` | **Construida** desde [`web/Dockerfile`](web/Dockerfile) | **Sí**, en `${PUERTO_WEB}`. Es el único |
| `api` | Construida desde `src/PortaFolioAcciones.Api/Dockerfile` | No: solo Caddy habla con ella |
| `postgres` | `postgres:18-alpine` | Solo en `127.0.0.1`, para herramientas desde Windows |

**Caddy no usa la imagen oficial y sirve la aplicación**, no solo la API. Se construye en dos etapas —Node
para compilar el Angular, Caddy para servirlo— así que la imagen final no lleva Node ni el código fuente. Y
enruta a la API solo lo que empieza por `/api`, más `/salud` y `/openapi`; todo lo demás es la aplicación,
con respaldo a `index.html` para que recargar en una ruta profunda no dé 404.

Los datos de PostgreSQL y de Caddy viven en **carpetas del host**, fuera del repositorio
(`../PortaFolioAcciones-datos/`), así que sobreviven a `docker compose down` y se pueden respaldar
desde Windows sin entrar en Docker.

Las variables van en un fichero `.env`, que **no se versiona** porque contiene la contraseña de la
base de datos. Hay una plantilla en [`.env.example`](.env.example).

## Copias de seguridad

```bat
respaldar.cmd                          vuelca la base y guarda los últimos 14 respaldos
respaldar.cmd -Retencion 30            con otra retención
restaurar.cmd                          restaura el más reciente en una base APARTE, para probarlo
restaurar.cmd -Fichero <ruta>          uno concreto, en la misma base de prueba
restaurar.cmd -Real                    ENCIMA de la base de verdad (pide confirmación)
programar-respaldo.cmd                 deja el respaldo programado a las 03:00 (una vez, como administrador)
```

**`restaurar.cmd` no toca la base buena por omisión**, y es lo que hace falta casi siempre: lo que hay que
comprobar es que el respaldo **se puede restaurar**, y para eso basta una base aparte. Restaurar encima de los
datos de verdad es una decisión, y por eso se pide con `-Real`, que además exige escribir `RESTAURAR` y hace
antes una copia de lo que hay.

Los respaldos van a `respaldos/`, **que el repositorio ignora**: un volcado contiene la base entera —nombres,
cuentas, posiciones y auditoría— en un solo fichero. Y la comprobación automática de datos personales **no lo
vería**, porque busca texto y un volcado está comprimido.

El respaldo deja un registro en `respaldos\respaldo.log`. Sin él, uno que lleva un mes fallando no se
distingue de uno que funciona.

## Cargar los datos desde los CSV del bróker

```bat
node herramientas\cargar-datos\cargar.mjs --simulacro   enseña lo que haría, sin escribir nada
node herramientas\cargar-datos\cargar.mjs               lo hace
```

**La herramienta no lleva ningún dato personal dentro**: lee `listado.csv`, `lotes.csv` y el mapa de
instrumentos, que están fuera del repositorio. Si los llevara, el repositorio se publicaría con el patrimonio
de la familia, que es justo lo que hay que evitar.

Entra como cada persona usando su clave temporal, porque **el alta de cartera crea para quien la pide**: el
administrador puede leer y modificar datos ajenos, pero no hay forma de crear una cartera a nombre de otro.

Por pantalla no sale ningún nombre —dice «persona 1», «persona 2»—: los nombres de entrada se derivan del
nombre real y una consola acaba copiada en un documento. Las claves temporales se escriben en
`credenciales-iniciales.txt`, que **también está fuera del repositorio**.

## Notas del entorno (importante)

Este equipo tiene particularidades que ya están resueltas, pero conviene conocer porque los síntomas
son confusos:

- **`dotnet build` a secas funciona.** La solución a los dos problemas de abajo está en
  [`Directory.Build.props`](Directory.Build.props) y no hay que pasar banderas a mano.

- **MSBuild y las tuberías con nombre.** El aislamiento de procesos de este equipo impide abrir
  tuberías con nombre, que es como MSBuild comunica sus nodos de trabajo. El síntoma era
  desconcertante: `Build FAILED` con **cero errores** y ningún mensaje. Se resuelve con
  `RestoreBuildInParallel=false` y `UseSharedCompilation=false` en `Directory.Build.props`.

- **Descargar paquetes NuGet necesita salida a Internet.** Si `dotnet restore` falla con
  `NU1301: No credentials are available in the security package`, es el aislamiento impidiendo el
  handshake TLS, no NuGet. Los paquetes ya descargados quedan en la caché local (`.nuget/`) y a
  partir de ahí se compila sin red.

- **Las pruebas no usan VSTest, y es a propósito.** El `testhost` de VSTest abre un handle del proceso
  padre para vigilar su salida, y aquí eso falla (`Test Run Aborted`). Por eso los proyectos de
  pruebas son **ejecutables autónomos** con **xunit v3** y Microsoft.Testing.Platform: se ejecutan
  solos, sin intermediario y con una pieza menos.

- **Docker Desktop está instalado** (comprobado el 2026-10-12: **29.8.2** con Compose **v5.6.0**).

- **El manifiesto de herramientas locales de .NET está bloqueado.** `dotnet tool install` sobre un
  manifiesto `.config/dotnet-tools.json` falla con «came from another computer and might be blocked»
  aunque el fichero no tenga marca de origen. Por eso `dotnet-ef` se instala en **`.herramientas/`**
  con `--tool-path`, que es igual de reproducible y se salta el problema.

- **`dotnet ef` necesita que la carpeta temporal esté dentro del proyecto.** Si se ejecuta tal cual,
  falla con `UnauthorizedAccessException` al crear una subcarpeta en la temporal del usuario. Se
  resuelve apuntando `TEMP` y `TMP` a una carpeta del proyecto antes de invocarlo. **`actualizar.cmd` ya lo
  hace**, así que no hay que acordarse.
- **Los `.cmd` tienen que tener saltos de línea CRLF.** `cmd.exe` los tolera en scripts simples, pero
  **rompe los bloques `for ( ... )` de varias líneas**, y el fallo que da —«The syntax of the command is
  incorrect»— no dice ni en qué fichero ni en qué línea. Si se editan con una herramienta que normalice a
  LF, hay que devolverlos a CRLF antes de ejecutarlos.

- **El código generado no se revisa.** Las migraciones de EF Core las escribe la herramienta, así que
  [`.editorconfig`](.editorconfig) las exime de las reglas de estilo. Sin eso, `TreatWarningsAsErrors`
  convierte en error de compilación cualquier detalle del generador, y el listón alto del código propio
  acaba estorbando en el código que no es nuestro.

## Privacidad

Este repositorio **se publica**, y nació de datos financieros reales de una familia. Las dos cosas se
llevan mal, así que hay reglas, y están comprobadas.

### Qué NO está en el repositorio

| | |
|---|---|
| Los estados de cuenta originales y su texto extraído | `FichierTravail/`, `herramientas/pdf-extract/salida*/` |
| Los CSV de posiciones, lotes y operaciones | `listado.csv`, `lotes.csv`, `actividades.csv`, `reconciliacion.csv`, `posiciones-todos-los-estados.csv` |
| La lista de instrumentos que se probó | `herramientas/prueba-cobertura/*.csv` — en su lugar va `instrumentos.example.csv` |
| Las credenciales | `.env` |
| La lista de cadenas que hay que vigilar | `.datos-sensibles.txt` |

Los CSV **no se pierden**: son **derivables**. Se regeneran volviendo a ejecutar las herramientas de
extracción sobre los estados de cuenta, que viven fuera del repositorio.

### Qué SÍ está, y por qué no identifica a nadie

La documentación narra **casos reales** —el desdoblamiento de MGK, la venta parcial de VEEV, los cuatro
fondos de Fundserv— porque son los que obligaron a tomar las decisiones. Se han **anonimizado**:

- **Los nombres de persona** son `Titular A` y `Titular B`. No son nombres inventados: son etiquetas, para
  que nadie los confunda con personas reales.
- **Los números de cuenta** son `RRSP-1`, `RRSP-2`, `RRSP-3`, `TFSA-1`, `TFSA-2`, `TFSA-3` y `TD-1`. La
  correspondencia con el tipo de cuenta se mantiene, así que las decisiones se siguen entendiendo.
- **Las cifras sí son las reales**, sin redondear. Por sí solas no identifican a nadie, y son las que
  hacen que las pruebas demuestren algo: el coste medio de VEEV que sobrevive a una venta parcial solo
  vale como prueba si es el de verdad.

### Comprobar antes de publicar

```cmd
herramientas\comprobar-datos-personales\ejecutar.cmd
```

Lee las cadenas prohibidas de `.datos-sensibles.txt` y las busca en **los ficheros que de verdad se
publicarían**: si hay repositorio git, le pregunta a git —rastreados más los no ignorados—, que es la
lista exacta. Devuelve `1` si encuentra algo.

Existe porque **anonimizar una vez no basta**. Cualquier línea nueva puede volver a colar un nombre o un
número de cuenta, y nadie lo notaría hasta que estuviera publicado y clonado. La comprobación convierte
«espero no haberme dejado ninguna» en «está comprobado».
