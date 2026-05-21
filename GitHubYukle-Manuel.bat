@echo off
chcp 65001 >nul
title PhonixFrame - GitHub Yukle (Manuel)
cd /d "%~dp0"

set "REPO_URL=https://github.com/ronekai/RonekaiFrame.git"
set "REPO_DESC=PhonixFrame — toplu e-ticaret urun gorseli sablonlayici (WPF .NET 8)"

echo.
echo === PhonixFrame GitHub yukleme ===
echo Depo: %REPO_URL%
echo.

where git >nul 2>&1
if errorlevel 1 (
    echo HATA: Git yok. winget install Git.Git
    pause
    exit /b 1
)

where gh >nul 2>&1
if errorlevel 1 (
    echo HATA: GitHub CLI yok. winget install GitHub.cli
    pause
    exit /b 1
)

gh auth status
if errorlevel 1 (
    echo HATA: Once: gh auth login
    pause
    exit /b 1
)

if not exist ".git" (
    echo Git baslatiliyor...
    git init
)

git remote get-url origin >nul 2>&1
if errorlevel 1 (
    echo origin ekleniyor...
    git remote add origin "%REPO_URL%"
)

git branch -M main 2>nul

git add -A
echo.
echo --- Staging durumu ---
git status --short
echo.

if not exist "commit-msg.txt" (
    echo PhonixFrame v1.0 — canli onizleme, platform cozunurlukleri, logo yonetimi> commit-msg.txt
    echo.>> commit-msg.txt
    echo - Rebrand to PhonixFrame>> commit-msg.txt
    echo - Live preview, 12 templates, platform export sizes>> commit-msg.txt
    echo - Logo PNG/JPEG/HEIC, new UI layout>> commit-msg.txt
)

git diff --cached --quiet
if errorlevel 1 (
    echo Commit olusturuluyor...
    git commit -F commit-msg.txt
) else (
    echo Yeni dosya yok — mevcut commit push edilecek.
)

echo.
echo Push ediliyor (origin main)...
git push -u origin main
if errorlevel 1 (
    echo main basarisiz — HEAD ile deneniyor...
    git push -u origin HEAD
)
if errorlevel 1 (
    echo master dalı deneniyor...
    git push -u origin master
)

gh repo edit ronekai/RonekaiFrame --description "%REPO_DESC%" 2>nul

echo.
echo --- Sonuc ---
gh repo view ronekai/RonekaiFrame --json url -q .url 2>nul
if errorlevel 1 gh repo view --json url -q .url
echo.
echo Tarayici: https://github.com/ronekai/RonekaiFrame
pause
