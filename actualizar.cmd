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
if not exist "respaldos" mkdir respaldos
for /f "tokens=1-4 delims=/- " %%a in ("%DATE%") do set "HOY=%%c-%%b-%%a"
docker compose exec -T postgres pg_dump -U %POSTGRES_USER% -d %POSTGRES_DB% > "respaldos\portafolio-%HOY%.sql"
if errorlevel 1 (
    echo La copia de seguridad ha fallado. NO se toca la base de datos.
    exit /b 1
)
echo Copia guardada en respaldos\portafolio-%HOY%.sql

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
    echo Restaura la copia de respaldos\portafolio-%HOY%.sql antes de seguir.
    exit /b 1
)

echo.
echo === Arrancando ===
docker compose up -d --build
echo.
echo Listo. Revisa el estado con:  docker compose ps
endlocal
