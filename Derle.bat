@echo off
setlocal
chcp 65001 >nul
title PhonixFrame - Derle
cd /d "%~dp0"

call _BuildCommon.bat Verify
if errorlevel 1 goto fail

call _BuildCommon.bat ReleaseLocks

echo.
echo ========================================
echo   PhonixFrame — derleme
echo   Klasor: %CD%
echo ========================================
echo.

call _BuildCommon.bat Restore
if errorlevel 1 goto fail

call _BuildCommon.bat Build
if errorlevel 1 goto fail

echo.
echo DERLEME BASARILI.
echo   Cikti: bin\Debug\net8.0-windows\PhonixFrame.exe
echo   Calistir: Calistir.bat
echo   GitHub:   GitHubYukle.bat
echo   Log:      build-output.txt
pause
exit /b 0

:fail
echo.
echo DERLEME BASARISIZ.
call _BuildCommon.bat ShowLog
echo.
echo Cozum sirasi:
echo   1. PhonixFrame / Cursor debug kapali olsun
echo   2. TamTemizlik.bat
echo   3. dotnet --version  (.NET 8 SDK gerekir)
pause
exit /b 1
