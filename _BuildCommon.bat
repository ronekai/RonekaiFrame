@echo off
REM ============================================================
REM  PhonixFrame — ortak derleme yardimcilari (_BuildCommon.bat)
REM  Diger .bat dosyalari bu betigi cagirir; dogrudan calistirmayin.
REM ============================================================
REM  call _BuildCommon.bat ReleaseLocks
REM  call _BuildCommon.bat Restore
REM  call _BuildCommon.bat Build
REM  call _BuildCommon.bat ShowLog
REM ============================================================
setlocal EnableDelayedExpansion
set "ROOT=%~dp0"
set "CSPROJ=%ROOT%RonekaiImageFramer.csproj"
set "EXE=%ROOT%bin\Debug\net8.0-windows\PhonixFrame.exe"
set "LOG=%ROOT%build-output.txt"

if /i "%~1"=="ReleaseLocks" goto ReleaseLocks
if /i "%~1"=="Restore" goto Restore
if /i "%~1"=="Build" goto Build
if /i "%~1"=="ShowLog" goto ShowLog
if /i "%~1"=="Verify" goto Verify
if /i "%~1"=="VerifyDotNet" goto VerifyDotNet
if /i "%~1"=="VerifyAssets" goto VerifyAssets
exit /b 1

:Verify
if not exist "%CSPROJ%" (
    echo HATA: RonekaiImageFramer.csproj bulunamadi.
    echo Klasor: %ROOT%
    exit /b 1
)
exit /b 0

:VerifyDotNet
where dotnet >nul 2>&1
if errorlevel 1 (
    echo HATA: .NET SDK bulunamadi.
    echo Kurulum: https://dotnet.microsoft.com/download/dotnet/8.0
    echo veya: winget install Microsoft.DotNet.SDK.8
    exit /b 1
)
for /f "tokens=1 delims=." %%a in ('dotnet --version 2^>nul') do set "DOTNET_MAJOR=%%a"
if not "%DOTNET_MAJOR%"=="8" (
    echo UYARI: .NET 8 SDK onerilir. Mevcut surum:
    dotnet --version
)
exit /b 0

:VerifyAssets
set "MISSING=0"
for %%f in (
    "filigram-08.svg"
    "filigram-09.svg"
    "nadir-figur-yatay-beyaz.svg"
    "nadir-figur-yatay-siyah.svg"
) do (
    if not exist "%ROOT%Assets\%%~f" (
        echo EKSIK ASSET: Assets\%%~f
        set "MISSING=1"
    )
)
if "!MISSING!"=="1" (
    echo.
    echo HATA: Varsayilan logo dosyalari eksik. GitHub'dan tam projeyi indirin.
    exit /b 1
)
echo Varsayilan logo assetleri tamam.
exit /b 0

:ReleaseLocks
echo [1/3] PhonixFrame ve derleme sunucusu kapatiliyor...
taskkill /IM PhonixFrame.exe /F >nul 2>&1
taskkill /IM RonekaiFrame.exe /F >nul 2>&1
dotnet build-server shutdown >nul 2>&1
timeout /t 2 >nul
exit /b 0

:Restore
call :Verify
if errorlevel 1 exit /b 1
echo [2/3] Paketler yukleniyor (dotnet restore)...
dotnet restore "%CSPROJ%" --force-evaluate >"%LOG%" 2>&1
if errorlevel 1 (
    echo RESTORE BASARISIZ. >>"%LOG%"
    exit /b 1
)
echo RESTORE BASARILI. >>"%LOG%"
exit /b 0

:Build
call :Verify
if errorlevel 1 exit /b 1
echo [3/3] Derleniyor (Debug)...
dotnet build "%CSPROJ%" -c Debug --no-restore >>"%LOG%" 2>&1
if errorlevel 1 (
    echo BUILD BASARISIZ. >>"%LOG%"
    exit /b 1
)
echo BUILD BASARILI. >>"%LOG%"
if not exist "%EXE%" (
    echo HATA: %EXE% olusmadi. >>"%LOG%"
    exit /b 1
)
exit /b 0

:ShowLog
if exist "%LOG%" (
    type "%LOG%"
) else (
    echo build-output.txt henuz yok.
)
exit /b 0
