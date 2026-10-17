@echo off
REM Restauracion de un respaldo.
REM
REM POR OMISION NO TOCA LA BASE DE VERDAD: restaura en una base aparte, la de comprobacion, que es lo que
REM hace falta para saber que el respaldo sirve. Para restaurar encima de los datos de verdad hay que
REM pedirlo con -Real, y entonces pide confirmacion.
REM
REM   restaurar.cmd                 el respaldo mas reciente, en la base de comprobacion
REM   restaurar.cmd -Fichero <ruta> uno concreto, en la base de comprobacion
REM   restaurar.cmd -Real           ENCIMA de la base de verdad (pide confirmacion)
setlocal
set "RAIZ=%~dp0"
cd /d "%RAIZ%"

powershell -NoProfile -ExecutionPolicy Bypass -File "herramientas\respaldo\restaurar.ps1" %*
exit /b %ERRORLEVEL%
