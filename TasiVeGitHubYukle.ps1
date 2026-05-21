# Masaustu -> Source\Repos\RonekaiFrame + GitHub yukleme
$ErrorActionPreference = "Stop"
$src = $PSScriptRoot
$dst = Join-Path $env:USERPROFILE "Source\Repos\RonekaiFrame"
$log = Join-Path $dst "tasi-ve-yukle-log.txt"

function Log($msg) {
    $line = "[$(Get-Date -Format 'HH:mm:ss')] $msg"
    Write-Host $line
    if (Test-Path (Split-Path $log)) { $line | Add-Content $log }
}

New-Item -ItemType Directory -Force -Path $dst | Out-Null
"" | Set-Content $log -Encoding UTF8
Log "Kaynak: $src"
Log "Hedef:  $dst"

$excludeDirs = @('bin', 'obj', '.vs', '.git')
Get-ChildItem $src -Recurse -File | Where-Object {
    $rel = $_.FullName.Substring($src.Length + 1)
    $parts = $rel -split '\\'
    -not ($parts | Where-Object { $excludeDirs -contains $_ })
} | ForEach-Object {
    $rel = $_.FullName.Substring($src.Length + 1)
    $target = Join-Path $dst $rel
    $dir = Split-Path $target -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    Copy-Item $_.FullName $target -Force
    Log "Kopyalandi: $rel"
}

Set-Location $dst
if (-not (Test-Path .git)) { git init | ForEach-Object { Log $_ } }
git add -A 2>&1 | ForEach-Object { Log $_ }
git commit -m "Initial release of RonekaiFrame" 2>&1 | ForEach-Object { Log $_ }

gh auth status 2>&1 | ForEach-Object { Log $_ }
gh repo create RonekaiFrame `
    --public `
    --description "RONEKAI.DEN icin toplu e-ticaret urun gorseli sablonlayici (WPF)" `
    --source=. `
    --remote=origin `
    --push 2>&1 | ForEach-Object { Log $_ }

if ($LASTEXITCODE -ne 0) {
    Log "RonekaiFrame dolu olabilir; RonekaiFrame-Studio deneniyor..."
    gh repo create RonekaiFrame-Studio `
        --public `
        --description "RONEKAI.DEN icin toplu e-ticaret urun gorseli sablonlayici (WPF)" `
        --source=. `
        --remote=origin `
        --push 2>&1 | ForEach-Object { Log $_ }
}

$url = gh repo view --json url -q .url 2>&1
Log "Repo: $url"
Log "Bitti. Log: $log"
notepad $log
