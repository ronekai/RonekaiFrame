@echo off
chcp 65001 >nul
title RONEKAI.DEN - Temizle ve Calistir
cd /d "%~dp0"

echo ========================================
echo  PROJE: %CD%
echo ========================================
echo.
echo --- csproj paket satirlari ---
findstr /i "SixLabors" RonekaiImageFramer.csproj
echo.
echo --- UiColorHelper (ilk 5 satir) ---
powershell -NoProfile -Command "Get-Content 'Ui\UiColorHelper.cs' -TotalCount 8"
echo.

echo [0/5] Program ve derleme sunucusu kapatiliyor...
taskkill /IM PhonixFrame.exe /F >nul 2>&1
taskkill /IM RonekaiFrame.exe /F >nul 2>&1
dotnet build-server shutdown >nul 2>&1
timeout /t 2 >nul

echo [1/5] bin ve obj siliniyor...
if exist "bin" rmdir /s /q "bin" 2>nul
if exist "obj" rmdir /s /q "obj" 2>nul
if exist "obj" (
    echo UYARI: obj silinemedi. Cursor/VS kapatin, TamTemizlik.bat deneyin.
    pause
    exit /b 1
)

echo [2/5] NuGet onbellek temizleniyor...
dotnet nuget locals http-cache --clear >nul 2>&1

echo [3/5] Paketler yukleniyor...
dotnet restore --force-evaluate
if errorlevel 1 goto hata

echo [4/5] Derleniyor...
dotnet build -c Debug --no-restore
if errorlevel 1 goto hata

echo.
echo BASARILI. Program aciliyor...
start "" "%CD%\bin\Debug\net8.0-windows\PhonixFrame.exe"
timeout /t 4 >nul
exit /b 0

:hata
echo.
echo DERLEME BASARISIZ.
echo Yukarida "3.1.11" ve "UiColorHelper" gorunmuyorsa yanlis klasordesiniz.
pause
exit /b 1
