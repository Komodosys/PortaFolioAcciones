@echo off
REM ============================================================================
REM  Lanzador de la prueba de cobertura de instrumentos.
REM
REM  Existe para no tener que recordar dos cosas cada vez:
REM
REM   1. -ExecutionPolicy Bypass, para ESTA ejecucion. Sin el, Windows bloquea el
REM      script por no estar firmado digitalmente ("is not digitally signed").
REM      NO cambia ninguna configuracion permanente del sistema: afecta solo a
REM      este proceso.
REM
REM   2. Usar PowerShell 7 (pwsh) si esta disponible. Windows PowerShell 5.1 usa
REM      .NET Framework y puede fallar al negociar TLS con la fuente de datos.
REM
REM  Uso:
REM     ejecutar.cmd                     usa instrumentos.csv de esta carpeta
REM     ejecutar.cmd otra-lista.csv      usa el CSV indicado
REM ============================================================================

setlocal
set "CARPETA=%~dp0"
set "ENTRADA=%~1"
if "%ENTRADA%"=="" set "ENTRADA=%CARPETA%instrumentos.csv"

if not exist "%ENTRADA%" (
    echo.
    echo No encuentro el CSV de entrada: "%ENTRADA%"
    echo Edita "instrumentos.csv" con tus instrumentos o pasa la ruta como argumento.
    echo.
    exit /b 1
)

where pwsh >nul 2>nul
if errorlevel 1 goto sin_pwsh

echo Usando PowerShell 7 ^(pwsh^)...
echo.
pwsh -NoProfile -ExecutionPolicy Bypass -File "%CARPETA%Test-Cobertura.ps1" -Entrada "%ENTRADA%"
goto fin

:sin_pwsh
echo PowerShell 7 no encontrado. Se usara Windows PowerShell 5.1.
echo AVISO: la 5.1 puede fallar al conectar con la fuente de datos ^(TLS^).
echo Para instalarlo:  winget install --id Microsoft.PowerShell
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%CARPETA%Test-Cobertura.ps1" -Entrada "%ENTRADA%"

:fin
set "CODIGO=%ERRORLEVEL%"
echo.
echo Proceso terminado con codigo %CODIGO%.
endlocal & exit /b %CODIGO%
