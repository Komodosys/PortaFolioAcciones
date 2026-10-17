@echo off
REM Compila la solucion y ejecuta las pruebas de todos los proyectos de tests.
REM
REM xunit v3 compila cada proyecto de pruebas como un EJECUTABLE autonomo (Microsoft.Testing.Platform),
REM asi que se ejecuta directamente y no hace falta VSTest. Es a proposito: el "testhost" de VSTest
REM necesita abrir un handle del proceso padre, y en este equipo eso falla.
setlocal enabledelayedexpansion
set "RAIZ=%~dp0"
set "FALLO=0"

echo === Compilando ===
dotnet build "%RAIZ%PortaFolioAcciones.slnx" --nologo -v q
if errorlevel 1 (
    echo.
    echo LA COMPILACION HA FALLADO
    exit /b 1
)

for %%P in (Domain.Tests Architecture.Tests Infrastructure.Tests) do (
    echo.
    echo === %%P ===
    "%RAIZ%tests\PortaFolioAcciones.%%P\bin\Debug\net10.0\PortaFolioAcciones.%%P.exe"
    if errorlevel 1 set "FALLO=1"
)

echo.
if "!FALLO!"=="1" (
    echo HAY PRUEBAS QUE FALLAN
) else (
    echo Todas las pruebas pasan
)
exit /b !FALLO!
