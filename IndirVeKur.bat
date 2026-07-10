@echo off
setlocal
chcp 65001 >nul
title PhonixFrame - GitHub Indir ve Kur
cd /d "%~dp0"

set "REPO_URL=https://github.com/ronekai/RonekaiFrame.git"
set "DEFAULT_TARGET=%USERPROFILE%\Source\Repos\RonekaiFrame"

echo.
echo ========================================
echo   PhonixFrame — GitHub'dan indir ve kur
echo   Depo: %REPO_URL%
echo ========================================
echo.

where git >nul 2>&1
if errorlevel 1 (
    echo Git bulunamadi. Once GitKur.bat calistirin.
    pause
    exit /b 1
)

REM Proje klasorunun icinden calistirildiysa: pull + kur
if exist "%~dp0RonekaiImageFramer.csproj" (
    echo Mevcut proje guncelleniyor...
    git pull origin main
    if errorlevel 1 (
        echo git pull basarisiz.
        pause
        exit /b 1
    )
    call "%~dp0Kur.bat"
    exit /b %ERRORLEVEL%
)

set "TARGET=%DEFAULT_TARGET%"
echo Hedef klasor (Enter = varsayilan):
echo   %DEFAULT_TARGET%
set /p "USER_TARGET=Klasor yolu: "
if not "%USER_TARGET%"=="" set "TARGET=%USER_TARGET%"

if exist "%TARGET%\.git" (
    echo.
    echo Guncelleniyor: %TARGET%
    pushd "%TARGET%"
    git pull origin main
    if errorlevel 1 (
        popd
        pause
        exit /b 1
    )
    popd
) else (
    if exist "%TARGET%\RonekaiImageFramer.csproj" (
        echo Klasorde proje var, clone atlaniyor.
    ) else (
        echo.
        echo Indiriliyor: %TARGET%
        git clone "%REPO_URL%" "%TARGET%"
        if errorlevel 1 (
            pause
            exit /b 1
        )
    )
)

echo.
pushd "%TARGET%"
call "%TARGET%\Kur.bat"
set "RC=%ERRORLEVEL%"
popd
exit /b %RC%
