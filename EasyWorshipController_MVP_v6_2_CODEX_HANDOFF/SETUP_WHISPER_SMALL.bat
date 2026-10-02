@echo off
cd /d "%~dp0"
title EasyWorship Local Whisper Setup
echo Starting Local Whisper setup (small.en)...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0SETUP_LOCAL_WHISPER.ps1" -Model small.en
set "RC=%ERRORLEVEL%"
echo.
if not "%RC%"=="0" (
  echo Setup FAILED with exit code %RC%.
  echo Send a photo of the error above.
) else (
  echo Setup completed successfully.
)
echo.
pause
exit /b %RC%
