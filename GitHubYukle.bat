@echo off
setlocal
chcp 65001 >nul
title PhonixFrame - GitHub Yukle
cd /d "%~dp0"

echo.
echo ========================================
echo   PhonixFrame — GitHub Yukle
echo   Depo: https://github.com/ronekai/RonekaiFrame
echo ========================================
echo.
echo Commit mesaji: commit-msg.txt (varsa)
echo Log: github-yukle-log.txt
echo.

where git >nul 2>&1
if errorlevel 1 (
    echo Git bulunamadi. Once GitKur.bat calistirin.
    pause
    exit /b 1
)

where gh >nul 2>&1
if errorlevel 1 (
    echo GitHub CLI (gh) yok: winget install GitHub.cli
    echo Sonra: gh auth login
    pause
    exit /b 1
)

choice /C EH /M "Push oncesi Derle.bat ile derleme yapilsin mi"
if not errorlevel 2 (
    call "%~dp0Derle.bat"
    if errorlevel 1 (
        echo Derleme basarisiz — push iptal.
        pause
        exit /b 1
    )
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0GitHubYukle.ps1"
exit /b %ERRORLEVEL%
