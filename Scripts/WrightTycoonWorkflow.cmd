@echo off
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0WrightTycoonWorkflow.ps1" %*
exit /b %errorlevel%
