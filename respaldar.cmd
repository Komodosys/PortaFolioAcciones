@echo off
REM Respaldo de la base de datos, con retencion.
REM
REM Se puede ejecutar a mano o dejar programado (ver programar-respaldo.cmd).
REM
REM Todo el trabajo lo hace el script de PowerShell: aqui solo se comprueba que este y se le pasa el relevo.
REM El resultado se apunta en respaldos\respaldo.log, y eso importa cuando lo ejecuta el Programador de
REM tareas: sin registro, un respaldo que lleva un mes fallando no se distingue de uno que funciona.
setlocal
set "RAIZ=%~dp0"
cd /d "%RAIZ%"

if not exist "respaldos" mkdir "respaldos"

echo. >> "respaldos\respaldo.log"
echo === %DATE% %TIME% === >> "respaldos\respaldo.log"

powershell -NoProfile -ExecutionPolicy Bypass -File "herramientas\respaldo\respaldar.ps1" %* >> "respaldos\respaldo.log" 2>&1
set "RESULTADO=%ERRORLEVEL%"

if not "%RESULTADO%"=="0" (
    echo   El respaldo ha FALLADO. Mira respaldos\respaldo.log
    exit /b %RESULTADO%
)

echo   Respaldo hecho. Detalle en respaldos\respaldo.log
endlocal
exit /b 0
