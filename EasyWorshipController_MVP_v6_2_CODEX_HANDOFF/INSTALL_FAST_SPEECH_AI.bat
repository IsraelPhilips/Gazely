@echo off
cd /d "%~dp0"
title Scripture Assistant - Speech AI Setup
echo Installing the fast offline speech engine...
echo.
echo This downloads the small Fastest model and the Balanced model.
echo After this, normal use can be offline.
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0SETUP_LOCAL_WHISPER.ps1" -Model fast
set "RC=%ERRORLEVEL%"
echo.
if not "%RC%"=="0" (
  echo Setup failed. Send a photo of the error above.
) else (
  echo Speech AI installed successfully.
  echo Next: run BUILD_AND_RUN.bat
)
echo.
pause
exit /b %RC%
