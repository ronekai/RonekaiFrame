@echo off
chcp 65001 >nul
title PhonixFrame - GitHub Yukle
cd /d "%~dp0"

echo PowerShell betik politikasi icin Bypass kullaniliyor...
echo (.ps1 dosyasini dogrudan calistirmayin — bu .bat dosyasini kullanin)
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0GitHubYukle.ps1"
if %ERRORLEVEL% neq 0 (
    echo.
    echo PowerShell betigi basarisiz — Manuel bat deneniyor...
    call "%~dp0GitHubYukle-Manuel.bat"
    exit /b %ERRORLEVEL%
)

exit /b %ERRORLEVEL%

