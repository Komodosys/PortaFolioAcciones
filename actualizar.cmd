@echo off
REM Actualiza la aplicacion a la version nueva.
REM
REM La base de datos SOLO cambia de estructura aqui, nunca sola y nunca al arrancar. El orden de los
REM pasos importa y por eso esta en un script: copia, parar, migrar, arrancar.
REM
REM Si la migracion falla, NO se arranca nada. Y como PostgreSQL aplica los cambios de estructura
REM dentro de una transaccion, una migracion a medias se deshace sola.
setlocal
set "RAIZ=%~dp0"
cd /d "%RAIZ%"

if not exist ".env" (
    echo No existe el fichero .env.
    echo Copia .env.example a .env y rellena la contrasena.
    exit /b 1
)

REM Lee las variables del .env, ignorando comentarios y lineas vacias.
REM
REM `eol=#` es lo que salta las lineas de comentario, y es la forma CORRECTA: la que habia antes
REM —`if not "%%A:~0,1%"=="#"`— no es sintaxis valida de cmd y hacia que el script fallara con
REM «The syntax of the command is incorrect» ANTES de ejecutar nada. Es decir: el procedimiento de
REM actualizacion documentado no llegaba ni a empezar.
for /f "usebackq eol=# tokens=1,* delims==" %%A in (".env") do (
    if not "%%A"=="" set "%%A=%%B"
)

set "CADENA=Host=localhost;Port=%PUERTO_POSTGRES%;Database=%POSTGRES_DB%;Username=%POSTGRES_USER%;Password=%POSTGRES_PASSWORD%"
set "ConnectionStrings__Portafolio=%CADENA%"

echo === 1/4 Comprobando que PostgreSQL esta en pie ===
docker compose up -d postgres
if errorlevel 1 exit /b 1

echo.
echo === 2/4 Copia de seguridad ===

REM **La copia la hace respaldar.cmd, y no este script.**
REM
REM Antes se hacia aqui redirigiendo la salida de `pg_dump` a un `portafolio-%HOY%.sql`, y eso dejo un fichero
REM de **670 bytes con la cabecera y NINGUNA tabla**: un respaldo con el nombre de un respaldo y sin nada
REM dentro. No fallaba, no avisaba, y la retencion no lo borraba nunca porque solo mira los `*.dump`.
REM
REM respaldar.cmd vuelca, **comprueba el volcado con pg_restore**, rota los antiguos y lo sube cifrado fuera
REM del equipo. Si algo de eso falla, devuelve error.
REM
REM Y si la copia falla **no se sigue**: parar la API y migrar sin un respaldo comprobado es apostar a que
REM hoy no pasa nada.
call "respaldar.cmd"
if errorlevel 1 (
    echo.
    echo La copia de seguridad ha fallado o no se ha podido comprobar. NO se toca la base de datos.
    exit /b 1
)
echo Copia de seguridad hecha y comprobada.

echo.
echo === 3/4 Parando la API ===
docker compose stop api

echo.
echo === 4/4 Aplicando migraciones pendientes ===

REM La carpeta temporal se apunta DENTRO del proyecto antes de llamar a la herramienta, y no es un
REM capricho: `dotnet ef` compila, y la compilacion falla con UnauthorizedAccessException al crear una
REM subcarpeta en la temporal del usuario. Sin esto, el procedimiento de actualizacion documentado falla
REM en este equipo —y falla DESPUES de haber parado la API, que es el peor momento—.
if not exist ".tmp" mkdir ".tmp"
set "TEMP=%RAIZ%.tmp"
set "TMP=%RAIZ%.tmp"
set "NUGET_PACKAGES=%RAIZ%.nuget\packages"

.herramientas\dotnet-ef\dotnet-ef.exe database update --project src\PortaFolioAcciones.Infrastructure --startup-project src\PortaFolioAcciones.Api
if errorlevel 1 (
    echo.
    echo LA MIGRACION HA FALLADO. La aplicacion NO se arranca.
    echo Restaura el respaldo mas reciente de respaldos\ con:  restaurar.cmd
    exit /b 1
)

echo.
echo === Arrancando ===
docker compose up -d --build
echo.
echo Listo. Revisa el estado con:  docker compose ps
endlocal
