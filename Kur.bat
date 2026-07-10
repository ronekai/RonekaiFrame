@echo off
setlocal
chcp 65001 >nul
title PhonixFrame - Kurulum
cd /d "%~dp0"

echo.
echo ========================================
echo   PhonixFrame — ilk kurulum / derleme
echo   Klasor: %CD%
echo ========================================
echo.

call _BuildCommon.bat Verify
if errorlevel 1 goto fail

call _BuildCommon.bat VerifyDotNet
if errorlevel 1 (
    echo.
    choice /C EH /M ".NET 8 SDK kurulum sayfasini acayim mi"
    if not errorlevel 2 start https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

call _BuildCommon.bat VerifyAssets
if errorlevel 1 goto fail

call _BuildCommon.bat ReleaseLocks

echo.
echo [1/2] NuGet paketleri...
call _BuildCommon.bat Restore
if errorlevel 1 goto fail

echo [2/2] Derleme...
call _BuildCommon.bat Build
if errorlevel 1 goto fail

echo.
echo ========================================
echo   KURULUM TAMAM
echo ========================================
echo.
echo   Calistir.bat           — programi ac
echo   Derle.bat              — yeniden derle
echo   TemizleVeCalistir.bat  — temiz derleme + ac
echo.
echo   Varsayilan logolar: Assets\ klasorunde (SVG)
echo   Cikti exe: bin\Debug\net8.0-windows\PhonixFrame.exe
echo.

choice /C EH /M "PhonixFrame simdi acilsin mi"
if not errorlevel 2 (
    start "" "%CD%\bin\Debug\net8.0-windows\PhonixFrame.exe"
    timeout /t 2 >nul
    exit /b 0
)

pause
exit /b 0

:fail
echo.
echo KURULUM BASARISIZ.
call _BuildCommon.bat ShowLog
echo.
echo Cozum: TamTemizlik.bat veya TemizleVeCalistir.bat
pause
exit /b 1
