@echo off
cd /d "%~dp0"

if not exist "%~dp0EasyWorshipController.exe" (
    echo EasyWorshipController.exe has not been built yet.
    echo Run BUILD_AND_RUN.bat first.
    echo.
    pause
    exit /b 1
)

start "" "%~dp0EasyWorshipController.exe"
exit /b 0
