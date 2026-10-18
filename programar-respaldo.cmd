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

REM Tres ajustes que `schtasks` no pone, y que en un equipo de casa son la diferencia entre "a las tres" y
REM "cuando se pueda":
REM
REM   StartWhenAvailable   si el equipo estaba apagado o suspendido a las tres, la tarea se ejecuta en
REM                        cuanto arranca. Sin esto, un dia con el equipo apagado es un dia SIN respaldo,
REM                        y el registro no lo dice: simplemente no hay linea. Un respaldo que falta en
REM                        silencio es peor que uno que falla y lo cuenta.
REM
REM   MultipleInstances    si una ejecucion se alarga, no se lanza otra encima: dos volcados a la vez
REM                        escriben en el mismo sitio y el que llega tarde falla sin motivo.
REM
REM   ExecutionTimeLimit   una hora. El limite por defecto son 72, y con "no lanzar otra encima" una
REM                        ejecucion colgada dejaria el respaldo parado TRES DIAS sin que nadie se entere.
REM                        Un volcado de este tamano tarda segundos: si pasa de una hora, esta colgado.
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$t = Get-ScheduledTask -TaskName 'PortaFolio - respaldo' -ErrorAction Stop;" ^
  "$t.Settings.StartWhenAvailable = $true;" ^
  "$t.Settings.MultipleInstances = 'IgnoreNew';" ^
  "$t.Settings.ExecutionTimeLimit = 'PT1H';" ^
  "$t | Set-ScheduledTask | Out-Null"

if errorlevel 1 (
    echo.
    echo   La tarea se ha creado, pero no se han podido poner sus ajustes. Vuelve a ejecutarlo como
    echo   administrador.
    exit /b 1
)

echo.
echo   Tarea creada, con tres ajustes que importan:
echo     - si el equipo estaba apagado a las 03:00, el respaldo se hace al arrancar
echo     - dos ejecuciones no se solapan
echo     - una ejecucion colgada se corta a la hora, no a los tres dias

echo.
echo   Tarea creada. Comprobala con:
echo     schtasks /query /tn "PortaFolio - respaldo"
echo.
echo   Y para probarla ahora mismo, sin esperar a las tres:
echo     schtasks /run /tn "PortaFolio - respaldo"
endlocal
exit /b 0
