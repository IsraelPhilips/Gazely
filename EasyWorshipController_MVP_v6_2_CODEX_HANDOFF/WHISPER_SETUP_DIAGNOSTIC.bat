@echo off
cd /d "%~dp0"
title EasyWorship Whisper Setup Diagnostics
echo Testing GitHub runtime URL...
curl.exe -I -L --fail --connect-timeout 15 "https://github.com/ggml-org/whisper.cpp/releases/download/b5130/whisper-bin-x64.zip"
echo.
echo GitHub test exit code: %ERRORLEVEL%
echo.
echo Testing Hugging Face model endpoint...
curl.exe -I -L --fail --connect-timeout 15 "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin?download=true"
echo.
echo Hugging Face test exit code: %ERRORLEVEL%
echo.
pause
