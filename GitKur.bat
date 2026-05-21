@echo off
chcp 65001 >nul
title Git Kurulumu - PhonixFrame
cd /d "%~dp0"

echo ========================================
echo  Git kontrolu ve kurulum
echo ========================================
echo.

call :FindGit
if defined GIT_EXE (
    echo Git bulundu: %GIT_EXE%
    "%GIT_EXE%" --version
    echo.
    echo PATH'e eklemek icin: Git kurulumunda "Git from the command line" secin
    echo veya yeni bir PowerShell/CMD penceresi acin.
    echo.
    echo Sonra GitHubYukle.bat calistirin.
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
        echo Kurulum tamamlandi. YENI bir CMD veya PowerShell acin, sonra:
        echo   cd "%CD%"
        echo   GitHubYukle.bat
        pause
        exit /b 0
    )
    echo winget kurulumu basarisiz veya iptal edildi.
)

echo.
echo Manuel kurulum:
echo   1) https://git-scm.com/download/win
echo   2) Kurulumda: "Git from the command line and also from 3rd-party software" secin
echo   3) Bilgisayari yeniden baslatmayin; sadece YENI terminal acin
echo   4) GitHubYukle.bat tekrar calistirin
echo.
start https://git-scm.com/download/win
pause
exit /b 1

:FindGit
set "GIT_EXE="
where git >nul 2>&1 && set "GIT_EXE=git" && goto :eof
if exist "C:\Program Files\Git\cmd\git.exe" set "GIT_EXE=C:\Program Files\Git\cmd\git.exe" && goto :eof
if exist "C:\Program Files\Git\bin\git.exe" set "GIT_EXE=C:\Program Files\Git\bin\git.exe" && goto :eof
if exist "C:\Program Files (x86)\Git\cmd\git.exe" set "GIT_EXE=C:\Program Files (x86)\Git\cmd\git.exe" && goto :eof
goto :eof
