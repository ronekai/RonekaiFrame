# PhonixFrame - Temiz derleme
$ErrorActionPreference = "Continue"
Set-Location $PSScriptRoot
$log = Join-Path $PSScriptRoot "build-output.txt"

"=== $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===" | Out-File $log -Encoding utf8
"Klasor: $PWD" | Out-File $log -Append -Encoding utf8
"" | Out-File $log -Append -Encoding utf8

"--- csproj paketleri ---" | Out-File $log -Append -Encoding utf8
Select-String -Path "RonekaiImageFramer.csproj" -Pattern "SixLabors" | ForEach-Object { $_.Line } | Out-File $log -Append -Encoding utf8

if (Test-Path bin) { Remove-Item -Recurse -Force bin }
if (Test-Path obj) { Remove-Item -Recurse -Force obj }

"`n--- dotnet restore ---" | Out-File $log -Append -Encoding utf8
dotnet restore --force-evaluate 2>&1 | Out-File $log -Append -Encoding utf8

"`n--- dotnet build ---" | Out-File $log -Append -Encoding utf8
dotnet build -c Debug 2>&1 | Out-File $log -Append -Encoding utf8

$exe = Join-Path $PSScriptRoot "bin\Debug\net8.0-windows\PhonixFrame.exe"
"`n--- sonuc ---" | Out-File $log -Append -Encoding utf8
if (Test-Path $exe) {
    "BASARILI: $exe" | Out-File $log -Append -Encoding utf8
    Write-Host "Derleme basarili. Program aciliyor..." -ForegroundColor Green
    Start-Process $exe
} else {
    "HATA: exe olusmadi. build-output.txt dosyasini acin." | Out-File $log -Append -Encoding utf8
    Write-Host "Derleme basarisiz. build-output.txt icine bakin." -ForegroundColor Red
    notepad $log
}

Write-Host "`nLog: $log"
Read-Host "Kapatmak icin Enter"
