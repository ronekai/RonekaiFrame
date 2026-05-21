@echo off
chcp 65001 >nul
title PhonixFrame - Derle
cd /d "%~dp0"

echo Program ve derleme sunucusu kapatiliyor...
taskkill /IM PhonixFrame.exe /F >nul 2>&1
taskkill /IM RonekaiFrame.exe /F >nul 2>&1
dotnet build-server shutdown >nul 2>&1
timeout /t 2 >nul

echo.
echo Derleniyor...
dotnet build -c Debug
set ERR=%ERRORLEVEL%

echo.
if %ERR% neq 0 (
    echo DERLEME BASARISIZ. Uygulama aciksa kapatip tekrar deneyin.
    pause
    exit /b %ERR%
)

echo DERLEME BASARILI.
pause
exit /b 0
