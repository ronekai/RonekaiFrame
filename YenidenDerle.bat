@echo off
setlocal
chcp 65001 >nul
title PhonixFrame - Yeniden Derle
cd /d "%~dp0"

call _BuildCommon.bat Verify
if errorlevel 1 goto fail

call _BuildCommon.bat ReleaseLocks

echo.
echo === Yeniden derleme ===
call _BuildCommon.bat Restore
if errorlevel 1 goto fail

call _BuildCommon.bat Build
if errorlevel 1 goto fail

echo.
echo BASARILI.
echo   Calistir.bat      — programi ac
echo   GitHubYukle.bat   — GitHub'a yukle
pause
exit /b 0

:fail
echo.
echo DERLEME BASARISIZ.
call _BuildCommon.bat ShowLog
echo Cozum: TamTemizlik.bat
pause
exit /b 1
