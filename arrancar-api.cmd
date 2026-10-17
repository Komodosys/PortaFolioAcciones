@echo off
REM Ejecuta la API directamente en Windows, sin Docker, para poder depurar con puntos de interrupcion.
REM
REM La base de datos SI sigue en Docker: este script solo evita tener que reconstruir la imagen en
REM cada cambio. Toma la cadena de conexion del fichero .env, que es el unico sitio donde esta la
REM contrasena.
setlocal
set "RAIZ=%~dp0"
cd /d "%RAIZ%"

if not exist ".env" (
    echo No existe el fichero .env.
    echo Copia .env.example a .env y rellena la contrasena.
    exit /b 1
)

REM `eol=#` salta las lineas de comentario. Es la forma correcta: la anterior —`"%%A:~0,1%"=="#"`— no es
REM sintaxis valida de cmd y hacia que el script fallara antes de ejecutar nada.
for /f "usebackq eol=# tokens=1,* delims==" %%A in (".env") do (
    if not "%%A"=="" set "%%A=%%B"
)

REM Desde Windows, PostgreSQL esta publicado en 127.0.0.1.
set "ConnectionStrings__Portafolio=Host=localhost;Port=%PUERTO_POSTGRES%;Database=%POSTGRES_DB%;Username=%POSTGRES_USER%;Password=%POSTGRES_PASSWORD%"
set "ASPNETCORE_ENVIRONMENT=Development"

echo === Asegurando que PostgreSQL esta en pie ===
docker compose up -d postgres

echo.
echo === Arrancando la API en local (Ctrl+C para parar) ===
dotnet run --project src\PortaFolioAcciones.Api
endlocal
