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
setlocal
set "ROOT=%~dp0"
set "CSPROJ=%ROOT%RonekaiImageFramer.csproj"
set "EXE=%ROOT%bin\Debug\net8.0-windows\PhonixFrame.exe"
set "LOG=%ROOT%build-output.txt"

if /i "%~1"=="ReleaseLocks" goto ReleaseLocks
if /i "%~1"=="Restore" goto Restore
if /i "%~1"=="Build" goto Build
if /i "%~1"=="ShowLog" goto ShowLog
if /i "%~1"=="Verify" goto Verify
exit /b 1

:Verify
if not exist "%CSPROJ%" (
    echo HATA: RonekaiImageFramer.csproj bulunamadi.
    echo Klasor: %ROOT%
    exit /b 1
)
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
