@echo off
chcp 65001 >nul
title PhonixFrame - GitHub TEK SURUM (eski gecmis silinir)
cd /d "%~dp0"

set "REPO_URL=https://github.com/ronekai/RonekaiFrame.git"
set "REPO_DESC=PhonixFrame — toplu e-ticaret urun gorseli sablonlayici (WPF .NET 8)"

echo.
echo ============================================
echo  UYARI: GitHub'daki ESKI commit gecmisi
echo  silinir. Yerine SADECE bu klasordeki
echo  guncel PhonixFrame kodu kalir.
echo ============================================
echo.
echo Depo: %REPO_URL%
echo.
choice /C EH /M "Devam etmek istiyor musunuz (E=evet H=hayir)"
if errorlevel 2 exit /b 0

where git >nul 2>&1 || (echo Git yok.& pause & exit /b 1)
where gh >nul 2>&1 || (echo gh yok.& pause & exit /b 1)

gh auth status || (echo gh auth login gerekli.& pause & exit /b 1)

if not exist ".git" git init

git remote get-url origin >nul 2>&1
if errorlevel 1 git remote add origin "%REPO_URL%"

echo.
echo Eski dal temizleniyor, tek commit olusturuluyor...
git checkout --orphan main-phonix 2>nul
if errorlevel 1 (
    echo orphan dal acilamadi — mevcut dosyalarla devam...
    git checkout -B main-phonix 2>nul
)

git add -A
git status --short

git commit -F commit-msg.txt
if errorlevel 1 (
    echo Commit zaten var veya bos — yine de push denenecek.
)

git branch -D main 2>nul
git branch -D master 2>nul
git branch -m main

echo.
echo GitHub'a zorla yukleniyor (force push)...
git push -f origin main
if errorlevel 1 git push -f origin HEAD

echo Eski master dali siliniyor (zorunlu degil, sadece main kalacak)...
git push origin --delete master 2>nul

echo Varsayilan dal: main
gh repo edit ronekai/RonekaiFrame --description "%REPO_DESC%" --default-branch main 2>nul

echo.
echo TAMAM — tek surum MAIN dalinda:
echo https://github.com/ronekai/RonekaiFrame
echo.
echo GitHub web: Settings ^> General ^> Default branch = main
echo master hala listede ise: dal listesinden master ^> Delete branch
echo.
pause
