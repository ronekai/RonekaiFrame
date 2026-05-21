@echo off

chcp 65001 >nul

title PhonixFrame

cd /d "%~dp0"



call :ReleaseLocks



echo.

echo Derleniyor (obj/bin silinmez - daha hizli, kilit sorunu yok)...

dotnet build -c Debug > build-log.txt 2>&1

set ERR=%ERRORLEVEL%



type build-log.txt



echo.

if %ERR% neq 0 (

    echo.

    echo ===== DERLEME HATASI =====

    echo Dosya kilitli ise: Visual Studio/Cursor kapatin veya TamTemizlik.bat deneyin.

    pause

    exit /b %ERR%

)



echo.

echo BASARILI. Program aciliyor...

start "" "%~dp0bin\Debug\net8.0-windows\PhonixFrame.exe"

timeout /t 2 >nul

exit /b 0



:ReleaseLocks

echo Program ve derleme sunucusu kapatiliyor...

taskkill /IM PhonixFrame.exe /F >nul 2>&1
taskkill /IM RonekaiFrame.exe /F >nul 2>&1

dotnet build-server shutdown >nul 2>&1

timeout /t 2 >nul

exit /b 0

