@echo off
setlocal EnableExtensions
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-bios.ps1" %*
exit /b %ERRORLEVEL%
