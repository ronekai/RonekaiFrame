@echo off
setlocal
chcp 65001 >nul
title PhonixFrame - Tam Temizlik
cd /d "%~dp0"

call _BuildCommon.bat Verify
if errorlevel 1 goto fail

echo ========================================
echo   TAM TEMIZLIK (bin + obj silinir)
echo   Once Cursor/VS debug'i durdurun
echo ========================================
echo.

call _BuildCommon.bat ReleaseLocks

echo bin ve obj siliniyor...
set RETRY=0
:TryDelete
set /a RETRY+=1
if exist bin rmdir /s /q bin 2>nul
if exist obj rmdir /s /q obj 2>nul
if exist obj (
    if %RETRY% lss 5 (
        echo obj kilitli — %RETRY%. deneme...
        call _BuildCommon.bat ReleaseLocks
        goto TryDelete
    )
    echo.
    echo HATA: obj silinemedi. PhonixFrame/Cursor kapatin.
    pause
    exit /b 1
)

echo Temizlik tamam.
echo.

call _BuildCommon.bat Restore
if errorlevel 1 goto fail

call _BuildCommon.bat Build
if errorlevel 1 goto fail

echo.
echo BASARILI.
echo   Calistir: Calistir.bat
echo   GitHub:   GitHubYukle.bat
pause
exit /b 0

:fail
echo.
echo DERLEME BASARISIZ.
call _BuildCommon.bat ShowLog
pause
exit /b 1
