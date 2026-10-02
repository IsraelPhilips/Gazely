@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "Activate-GazelyUpdate.ps1"
if errorlevel 1 (
    pause
    exit /b 1
)
if not exist "Gazely.exe" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "Build-EasyWorshipController.ps1" -OutputFileName "Gazely.exe"
    if errorlevel 1 (
        echo Gazely could not be built. Review the error above.
        pause
        exit /b 1
    )
)
start "" "Gazely.exe"
