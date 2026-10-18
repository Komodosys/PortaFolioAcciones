@echo off
REM Compila la solucion y ejecuta las pruebas de todos los proyectos de tests, mas las comprobaciones que
REM no son pruebas unitarias pero cazan fallos que ninguna prueba ve.
REM
REM xunit v3 compila cada proyecto de pruebas como un EJECUTABLE autonomo (Microsoft.Testing.Platform),
REM asi que se ejecuta directamente y no hace falta VSTest. Es a proposito: el "testhost" de VSTest
REM necesita abrir un handle del proceso padre, y en este equipo eso falla.
setlocal enabledelayedexpansion
set "RAIZ=%~dp0"
set "FALLO=0"
cd /d "%RAIZ%"

REM La bandera `-m:1` NO es opcional en este equipo, y sin ella el fallo es de los que hacen perder una tarde:
REM "Build FAILED" con CERO errores y sin ningun mensaje. El aislamiento no deja a MSBuild abrir tuberias con
REM nombre para sus nodos de trabajo, y con mas de uno la compilacion no llega a empezar.
REM
REM Si algun dia se ejecuta esto SIN salida a Internet, añadir tambien `-p:NuGetAudit=false`: la auditoria de
REM vulnerabilidades de NuGet se queda sin poder consultar su listado y ese aviso (NU1900) lo convierte en error
REM TreatWarningsAsErrors. No se deja puesta por defecto a proposito: apagaria una comprobacion de dependencias
REM que este proyecto quiere tener.
echo === Compilando ===
dotnet build "%RAIZ%PortaFolioAcciones.slnx" --nologo -v q -m:1
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

REM Estas dos comprobaciones existen porque los fallos que buscan NO los ve ninguna prueba: no rompen
REM nada, solo hacen que la pantalla enseñe una clave en crudo o un color que no es.
echo.
echo === Idiomas: claves usadas, repetidas y en los tres idiomas ===
node "%RAIZ%herramientas\comprobar-idiomas\comprobar.mjs"
if errorlevel 1 set "FALLO=1"

echo.
echo === Estilos: clases de Tailwind que se pisan entre si ===
node "%RAIZ%herramientas\comprobar-estilos\comprobar.mjs"
if errorlevel 1 set "FALLO=1"

echo.
if "!FALLO!"=="1" (
    echo HAY COMPROBACIONES QUE FALLAN
) else (
    echo Todas las pruebas y comprobaciones pasan
)
exit /b !FALLO!
