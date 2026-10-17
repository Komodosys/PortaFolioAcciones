@echo off
REM Comprueba que no haya datos personales en lo que se va a publicar.
REM
REM Se ejecuta con -ExecutionPolicy Bypass porque en este equipo los scripts descargados no estan
REM firmados y PowerShell los bloquea; no cambia la politica del sistema, solo esta invocacion.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0comprobar.ps1"
exit /b %errorlevel%
