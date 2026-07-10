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
echo Commit mesaji: commit-msg.txt ^(varsa^)
echo Log: github-yukle-log.txt
echo.

call :FindGit
if not defined GIT_EXE (
    echo Git bulunamadi. Once GitKur.bat calistirin.
    goto done_fail
)

call :FindGh
if not defined GH_EXE (
    echo GitHub CLI ^(gh^) bulunamadi.
    echo Kurulum: winget install GitHub.cli
    echo Sonra: gh auth login
    goto done_fail
)

set /p "BUILD_FIRST=Push oncesi Derle.bat ile derleme yapilsin mi? [E/H]: "
if /i "%BUILD_FIRST%"=="E" (
    call "%~dp0Derle.bat"
    if errorlevel 1 (
        echo Derleme basarisiz — push iptal.
        goto done_fail
    )
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0GitHubYukle.ps1"
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" goto done_fail
goto done_ok

:FindGit
set "GIT_EXE="
where git >nul 2>&1 && set "GIT_EXE=git" && goto :eof
if exist "C:\Program Files\Git\cmd\git.exe" set "GIT_EXE=C:\Program Files\Git\cmd\git.exe" && goto :eof
if exist "C:\Program Files\Git\bin\git.exe" set "GIT_EXE=C:\Program Files\Git\bin\git.exe" && goto :eof
goto :eof

:FindGh
set "GH_EXE="
where gh >nul 2>&1 && set "GH_EXE=gh" && goto :eof
if exist "C:\Program Files\GitHub CLI\gh.exe" set "GH_EXE=C:\Program Files\GitHub CLI\gh.exe" && goto :eof
goto :eof

:done_ok
echo.
echo GitHub yukleme tamamlandi.
pause
exit /b 0

:done_fail
echo.
echo GitHub yukleme basarisiz veya iptal edildi.
echo Detay: github-yukle-log.txt
pause
exit /b 1
