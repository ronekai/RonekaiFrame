@echo off

chcp 65001 >nul

title PhonixFrame - Kapat ve Yeniden Derle

cd /d "%~dp0"



call :ReleaseLocks



echo.

echo Derleniyor...

dotnet build -c Debug

set ERR=%ERRORLEVEL%



echo.

if %ERR% neq 0 (

    echo DERLEME BASARISIZ.

    echo.

    echo Cozum:

    echo   1. Cursor/Visual Studio kapatin

    echo   2. Gorev Yoneticisi: PhonixFrame ve dotnet sonlandir

    echo   3. TamTemizlik.bat calistirin

    pause

    exit /b %ERR%

)



echo.

echo BASARILI. Calistirmak icin: dotnet run  veya  Calistir.bat

pause

exit /b 0



:ReleaseLocks

echo Acik PhonixFrame ve derleme sunucusu kapatiliyor...

taskkill /IM PhonixFrame.exe /F >nul 2>&1
taskkill /IM RonekaiFrame.exe /F >nul 2>&1

dotnet build-server shutdown >nul 2>&1

timeout /t 2 >nul

exit /b 0

