@echo off
cd /d "%~dp0"

echo Building EasyWorship Controller...
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-EasyWorshipController.ps1"

if errorlevel 1 (
    echo.
    echo Build failed.
    pause
    exit /b 1
)

echo.
echo Starting controller...
start "" "%~dp0EasyWorshipController.exe"
exit /b 0
