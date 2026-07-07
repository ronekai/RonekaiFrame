@echo off
setlocal
chcp 65001 >nul
title PhonixFrame - Git Kurulumu
cd /d "%~dp0"

echo ========================================
echo   Git kontrolu ve kurulum
echo ========================================
echo.

call :FindGit
if defined GIT_EXE (
    echo Git bulundu: %GIT_EXE%
    "%GIT_EXE%" --version
    echo.
    where gh >nul 2>&1
    if errorlevel 1 (
        echo GitHub CLI (gh) yok. Kurmak icin:
        echo   winget install GitHub.cli
        echo   gh auth login
    ) else (
        gh --version
        echo.
        echo Hazir. GitHubYukle.bat calistirabilirsiniz.
    )
    pause
    exit /b 0
)

echo Git PATH'te yok. Kurulum baslatiliyor...
echo.

where winget >nul 2>&1
if not errorlevel 1 (
    echo winget ile Git.Git kuruluyor...
    winget install --id Git.Git -e --source winget --accept-package-agreements --accept-source-agreements
    if not errorlevel 1 (
        echo.
        echo Kurulum tamam. YENI bir CMD acin, sonra:
        echo   cd "%CD%"
        echo   GitKur.bat
        echo   GitHubYukle.bat
        pause
        exit /b 0
    )
)

echo Manuel: https://git-scm.com/download/win
echo Kurulumda "Git from the command line" secin.
start https://git-scm.com/download/win
pause
exit /b 1

:FindGit
set "GIT_EXE="
where git >nul 2>&1 && set "GIT_EXE=git" && goto :eof
if exist "C:\Program Files\Git\cmd\git.exe" set "GIT_EXE=C:\Program Files\Git\cmd\git.exe" && goto :eof
if exist "C:\Program Files\Git\bin\git.exe" set "GIT_EXE=C:\Program Files\Git\bin\git.exe" && goto :eof
goto :eof
