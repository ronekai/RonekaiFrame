@echo off
setlocal
chcp 65001 >nul
title PhonixFrame - Calistir
cd /d "%~dp0"

call _BuildCommon.bat Verify
if errorlevel 1 goto fail

call _BuildCommon.bat ReleaseLocks

echo.
echo ========================================
echo   PhonixFrame — derle ve calistir
echo ========================================
echo.

call _BuildCommon.bat Restore
if errorlevel 1 goto fail

call _BuildCommon.bat Build
if errorlevel 1 goto fail

set "EXE=%~dp0bin\Debug\net8.0-windows\PhonixFrame.exe"
echo Program aciliyor...
start "" "%EXE%"
timeout /t 2 >nul
exit /b 0

:fail
echo.
echo DERLEME BASARISIZ.
call _BuildCommon.bat ShowLog
echo.
echo Dosya kilitli ise TamTemizlik.bat deneyin.
pause
exit /b 1
