@echo off
chcp 65001 >nul
title PhonixFrame - Masaustunden kopyala
set "SRC=%USERPROFILE%\Desktop\RonekaiImageFramer"
set "DST=%USERPROFILE%\Source\Repos\RonekaiFrame"

if not exist "%SRC%" (
    echo Masaustu klasoru bulunamadi: %SRC%
    pause
    exit /b 1
)

echo Kaynak: %SRC%
echo Hedef:  %DST%
echo.

robocopy "%SRC%" "%DST%" /E /XD bin obj .vs .git /XF build-log.txt github-push-result.txt tasi-ve-yukle-log.txt

if not exist "%DST%\RonekaiImageFramer.csproj" (
    echo HATA: csproj kopyalanmadi.
    pause
    exit /b 1
)

echo.
echo Kopyalama tamam. Simdi GitHubYukle.bat calistirin.
pause
