@echo off
setlocal
chcp 65001 >nul
title PhonixFrame - Masaustu Kurulum
cd /d "%~dp0"

set "INSTALL_DIR=%LOCALAPPDATA%\PhonixFrame"
set "DESKTOP=%USERPROFILE%\Desktop"
set "SHORTCUT=%DESKTOP%\PhonixFrame.lnk"

echo.
echo ========================================
echo   PhonixFrame — masaustu kurulum
echo ========================================
echo.

call _BuildCommon.bat Verify
if errorlevel 1 goto fail

call _BuildCommon.bat VerifyDotNet
if errorlevel 1 goto fail

call _BuildCommon.bat ReleaseLocks

echo [1/3] Release derleme ve yayinlama...
dotnet publish "%~dp0RonekaiImageFramer.csproj" -c Release -o "%INSTALL_DIR%" >"%~dp0build-output.txt" 2>&1
if errorlevel 1 goto fail

if not exist "%INSTALL_DIR%\PhonixFrame.exe" (
    echo HATA: PhonixFrame.exe olusmadi.
    goto fail
)

echo [2/3] Masaustu kisayolu olusturuluyor...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$s = (New-Object -ComObject WScript.Shell).CreateShortcut('%SHORTCUT%');" ^
  "$s.TargetPath = '%INSTALL_DIR%\PhonixFrame.exe';" ^
  "$s.WorkingDirectory = '%INSTALL_DIR%';" ^
  "$s.Description = 'PhonixFrame - urun gorseli sablonlayici';" ^
  "$s.Save()"
if errorlevel 1 goto fail

echo [3/3] Tamamlandi.
echo.
echo   Program: %INSTALL_DIR%\PhonixFrame.exe
echo   Kisayol: %SHORTCUT%
echo.
echo Masaustundeki PhonixFrame simgesinden acabilirsiniz.
echo Guncellemek icin bu betigi tekrar calistirin.
echo.
pause
exit /b 0

:fail
echo.
echo KURULUM BASARISIZ.
if exist "%~dp0build-output.txt" type "%~dp0build-output.txt"
pause
exit /b 1
