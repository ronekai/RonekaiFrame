@echo off

chcp 65001 >nul

title PhonixFrame - Tam temizlik ve derleme

cd /d "%~dp0"



echo ========================================

echo  TAM TEMIZLIK (bin + obj silinir)

echo  Once Cursor/VS icindeki debug'i durdurun

echo ========================================

echo.



call :ReleaseLocks



echo bin ve obj siliniyor...

set RETRY=0

:TryDelete

set /a RETRY+=1

if exist bin rmdir /s /q bin 2>nul

if exist obj rmdir /s /q obj 2>nul

if exist obj (

    if %RETRY% lss 5 (

        echo obj hala kilitli, %RETRY%. deneme - 2 sn bekleniyor...

        call :ReleaseLocks

        goto TryDelete

    )

    echo.

    echo HATA: obj silinemedi - dosya baska programda acik.

    echo Cursor/Visual Studio KAPATIN, Gorev Yoneticisi ^> dotnet sonlandir

    pause

    exit /b 1

)



echo Temizlik tamam.

echo.

dotnet restore

dotnet build -c Debug

if errorlevel 1 (

    pause

    exit /b 1

)



echo.

echo BASARILI.

pause

exit /b 0



:ReleaseLocks

taskkill /IM PhonixFrame.exe /F >nul 2>&1
taskkill /IM RonekaiFrame.exe /F >nul 2>&1

dotnet build-server shutdown >nul 2>&1

timeout /t 2 >nul

exit /b 0

