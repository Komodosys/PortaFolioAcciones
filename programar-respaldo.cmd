@echo off
REM Deja el respaldo programado: todos los dias a las 03:00.
REM
REM **Hay que ejecutarlo como administrador**, y solo hace falta una vez.
REM
REM La tarea corre con la sesion de quien la crea, asi que **Docker Desktop tiene que estar arrancado** a
REM esa hora. Si no lo esta, la tarea fallara y lo dejara escrito en respaldos\respaldo.log: por eso el
REM respaldo escribe un registro en vez de callarse.
REM
REM Para quitarla:
REM   schtasks /delete /tn "PortaFolio - respaldo" /f
setlocal
set "RAIZ=%~dp0"

echo.
echo   Se va a programar el respaldo de PortaFolio todos los dias a las 03:00.
echo.

schtasks /create /tn "PortaFolio - respaldo" /tr "\"%RAIZ%respaldar.cmd\"" /sc daily /st 03:00 /f

if errorlevel 1 (
    echo.
    echo   No se ha podido crear la tarea. Si dice que hace falta elevacion, abre esta ventana como
    echo   administrador y vuelve a ejecutarlo.
    exit /b 1
)

echo.
echo   Tarea creada. Comprobala con:
echo     schtasks /query /tn "PortaFolio - respaldo"
echo.
echo   Y para probarla ahora mismo, sin esperar a las tres:
echo     schtasks /run /tn "PortaFolio - respaldo"
endlocal
exit /b 0
