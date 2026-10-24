@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Server-R6.8.ps1" %*
if errorlevel 1 (
 echo Server build stopped. Review the error and build-logs.
 pause
 exit /b 1
)
pause
