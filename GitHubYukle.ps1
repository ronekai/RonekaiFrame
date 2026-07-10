# PhonixFrame - GitHub'a son surumu yukle
# Cagiran: GitHubYukle.bat
# Log: github-yukle-log.txt
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Continue"
$root = if ($PSScriptRoot) { $PSScriptRoot } else { Get-Location }
$log = Join-Path $root "github-yukle-log.txt"
Set-Location $root

function Log($m) {
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $m"
    Add-Content -Path $log -Value $line -Encoding UTF8
    Write-Host $line
}

"" | Set-Content $log -Encoding UTF8
Log "=== PhonixFrame GitHub yukleme ==="
Log "Klasor: $root"

function Find-Exe([string]$name, [string[]]$fallbacks) {
    try { return (Get-Command $name -ErrorAction Stop).Source } catch {}
    foreach ($p in $fallbacks) {
        if (Test-Path $p) { return $p }
    }
    return $null
}

$git = Find-Exe "git" @(
    "C:\Program Files\Git\cmd\git.exe",
    "C:\Program Files\Git\bin\git.exe"
)
if (-not $git) {
    Log "HATA: Git yok. GitKur.bat veya winget install Git.Git"
    Read-Host "Enter ile kapat"
    exit 1
}

$gh = Find-Exe "gh" @(
    "C:\Program Files\GitHub CLI\gh.exe"
)
if (-not $gh) {
    Log "HATA: GitHub CLI yok. winget install GitHub.cli"
    Log "Sonra: gh auth login"
    Read-Host "Enter ile kapat"
    exit 1
}

& $gh auth status 2>&1 | ForEach-Object { Log $_ }
if ($LASTEXITCODE -ne 0) {
    Log "HATA: Once: gh auth login"
    Read-Host "Enter ile kapat"
    exit 1
}

$csproj = Join-Path $root "RonekaiImageFramer.csproj"
if (-not (Test-Path $csproj)) {
    Log "HATA: RonekaiImageFramer.csproj bulunamadi."
    Read-Host "Enter ile kapat"
    exit 1
}

if (-not (Test-Path (Join-Path $root ".git"))) {
    Log "git init..."
    & $git init 2>&1 | ForEach-Object { Log $_ }
    & $git branch -M main 2>&1 | ForEach-Object { Log $_ }
}

$repoUrl = "https://github.com/ronekai/RonekaiFrame.git"
$repoDesc = "PhonixFrame - toplu e-ticaret urun gorseli sablonlayici (WPF .NET 8)"

$originUrl = & $git remote get-url origin 2>$null
if (-not $originUrl) {
    Log "origin ekleniyor: $repoUrl"
    & $git remote add origin $repoUrl 2>&1 | ForEach-Object { Log $_ }
}

& $git add -A 2>&1 | ForEach-Object { Log $_ }
& $git status --short 2>&1 | ForEach-Object { Log $_ }

$commitFile = Join-Path $root "commit-msg.txt"
$porcelain = & $git status --porcelain 2>&1
if ($porcelain) {
    $assetFiles = @(
        "Assets/filigram-08.svg",
        "Assets/filigram-09.svg",
        "Assets/nadir-figur-yatay-beyaz.svg",
        "Assets/nadir-figur-yatay-siyah.svg"
    )
    foreach ($a in $assetFiles) {
        $full = Join-Path $root $a
        if (-not (Test-Path $full)) {
            Log "UYARI: Eksik asset - $a (baska PC kurulumu icin gerekli)"
        }
    }

    if (Test-Path $commitFile) {
        Log "Commit: commit-msg.txt"
        & $git commit -F $commitFile 2>&1 | ForEach-Object { Log $_ }
    } else {
        $msg = "PhonixFrame - guncel surum"
        Log "Commit: $msg"
        & $git commit -m $msg 2>&1 | ForEach-Object { Log $_ }
    }
} else {
    Log "Degisiklik yok - mevcut commit push edilecek."
}

& $git branch -M main 2>&1 | Out-Null

Log "Push (origin main)..."
& $git push -u origin main 2>&1 | ForEach-Object { Log $_ }
if ($LASTEXITCODE -ne 0) {
    & $git push -u origin HEAD 2>&1 | ForEach-Object { Log $_ }
}

& $gh repo edit ronekai/RonekaiFrame --description $repoDesc --default-branch main 2>&1 | ForEach-Object { Log $_ }

$url = & $gh repo view ronekai/RonekaiFrame --json url -q .url 2>&1
if (-not $url -or "$url" -match "error|fatal") {
    $url = & $gh repo view --json url -q .url 2>&1
}

Log "Repo: $url"
Log "=== Bitti ==="
Log "Log: $log"
Read-Host "Enter ile kapat"
