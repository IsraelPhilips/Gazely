@echo off
cd /d "%~dp0"
title Scripture Assistant - Fastest Speech Model
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0SETUP_LOCAL_WHISPER.ps1" -Model tiny.en
set "RC=%ERRORLEVEL%"
echo.
pause
exit /b %RC%
