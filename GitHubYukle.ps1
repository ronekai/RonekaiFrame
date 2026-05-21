# PhonixFrame — GitHub'a son surumu yukle
# Log: github-yukle-log.txt
$ErrorActionPreference = "Continue"
$root = if ($PSScriptRoot) { $PSScriptRoot } else { "C:\Users\mkaskara\Source\Repos\RonekaiFrame" }
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

$git = $null
try { $git = (Get-Command git -ErrorAction Stop).Source } catch {}
if (-not $git) {
    foreach ($p in @(
        "C:\Program Files\Git\cmd\git.exe",
        "C:\Program Files\Git\bin\git.exe"
    )) {
        if (Test-Path $p) { $git = $p; break }
    }
}
if (-not $git) {
    Log "HATA: Git yok. winget install Git.Git"
    Read-Host "Enter ile kapat"
    exit 1
}

$gh = $null
try { $gh = (Get-Command gh -ErrorAction Stop).Source } catch {}
if (-not $gh) {
    Log "HATA: GitHub CLI (gh) yok. winget install GitHub.cli && gh auth login"
    Read-Host "Enter ile kapat"
    exit 1
}

& $gh auth status 2>&1 | ForEach-Object { Log $_ }
if ($LASTEXITCODE -ne 0) {
    Log "HATA: Once: gh auth login"
    Read-Host "Enter ile kapat"
    exit 1
}

if (-not (Test-Path (Join-Path $root "RonekaiImageFramer.csproj"))) {
    Log "HATA: RonekaiImageFramer.csproj bulunamadi."
    Read-Host "Enter ile kapat"
    exit 1
}

if (-not (Test-Path (Join-Path $root ".git"))) {
    & $git init 2>&1 | ForEach-Object { Log $_ }
    & $git branch -M main 2>&1 | ForEach-Object { Log $_ }
}

& $git add -A 2>&1 | ForEach-Object { Log $_ }
& $git status --short 2>&1 | ForEach-Object { Log $_ }

$commitMsg = @"
PhonixFrame v1.0 — canli onizleme, platform cozunurlukleri, logo yonetimi

- Rebrand to PhonixFrame (exe, UI, PhonixFrame_* output folders)
- Live DEMO preview, redesigned UI (preview+log top, settings bottom)
- 12 templates with px labels in UI
- Platform export sizes (Instagram, WhatsApp, Sahibinden, ecommerce, etc.)
- Custom image brand text; default vs custom logo (PNG/JPEG/HEIC)
- Login gate, HEIC/HEIF batch support, subfolder scan
"@

$porcelain = & $git status --porcelain 2>&1
if ($porcelain) {
    & $git commit -m $commitMsg 2>&1 | ForEach-Object { Log $_ }
} else {
    Log "Commit atlanadi: degisiklik yok."
}

$repoUrl = "https://github.com/ronekai/RonekaiFrame.git"
$repoDesc = "PhonixFrame — toplu e-ticaret urun gorseli sablonlayici (WPF .NET 8)"

$originUrl = & $git remote get-url origin 2>$null
if (-not $originUrl) {
    Log "origin yok — mevcut GitHub reposuna baglaniyor: $repoUrl"
    & $git remote add origin $repoUrl 2>&1 | ForEach-Object { Log $_ }
}

& $git branch -M main 2>&1 | Out-Null

Log "Push ediliyor..."
& $git push -u origin main 2>&1 | ForEach-Object { Log $_ }
if ($LASTEXITCODE -ne 0) {
    & $git push -u origin HEAD 2>&1 | ForEach-Object { Log $_ }
}
if ($LASTEXITCODE -ne 0) {
    & $git push -u origin master 2>&1 | ForEach-Object { Log $_ }
}

& $gh repo edit ronekai/RonekaiFrame --description $repoDesc 2>&1 | ForEach-Object { Log $_ }

$url = & $gh repo view --json url -q .url 2>&1
Log "Repo URL: $url"
Log "=== Bitti ==="
Log "Log: $log"

Read-Host "Enter ile kapat"
