@echo off
setlocal
chcp 65001 >nul
title PhonixFrame - Temizle ve Calistir
cd /d "%~dp0"

call _BuildCommon.bat Verify
if errorlevel 1 goto fail

echo ========================================
echo   PhonixFrame — temizlik + calistir
echo   Klasor: %CD%
echo ========================================
echo.

call _BuildCommon.bat ReleaseLocks

echo [1/4] bin ve obj siliniyor...
if exist bin rmdir /s /q bin 2>nul
if exist obj rmdir /s /q obj 2>nul
if exist obj (
    echo UYARI: obj silinemedi. TamTemizlik.bat deneyin.
    pause
    exit /b 1
)

echo [2/4] NuGet onbellek temizleniyor...
dotnet nuget locals http-cache --clear >nul 2>&1

echo [3/4] Paketler...
call _BuildCommon.bat Restore
if errorlevel 1 goto fail

echo [4/4] Derleme...
call _BuildCommon.bat Build
if errorlevel 1 goto fail

set "EXE=%CD%\bin\Debug\net8.0-windows\PhonixFrame.exe"
echo.
echo BASARILI. Program aciliyor...
start "" "%EXE%"
timeout /t 2 >nul
exit /b 0

:fail
echo.
echo DERLEME BASARISIZ.
call _BuildCommon.bat ShowLog
pause
exit /b 1
