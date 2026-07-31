# PhonixFrame

**PhonixFrame** — Windows (.NET 8 WPF) masaüstü uygulaması. E-ticaret ve ilan görsellerinizi toplu olarak şablonlara oturtur; canlı önizleme, platform çözünürlükleri ve logo yönetimi içerir.

> Önceki ad: RonekaiFrame. GitHub deposu `RonekaiFrame` altında kalabilir; uygulama adı ve exe artık **PhonixFrame**'dir.

## Hızlı başlangıç

### Bu bilgisayarda (geliştirme)

```powershell
cd "$env:USERPROFILE\Source\Repos\RonekaiFrame"
taskkill /IM PhonixFrame.exe /F 2>$null
dotnet build
dotnet run
```

veya **`Calistir.bat`** / **`Derle.bat`** dosyalarına çift tıklayın.

### Başka bilgisayarda (ilk kurulum)

1. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) kurun (veya `winget install Microsoft.DotNet.SDK.8`)
2. [Git](https://git-scm.com/download/win) kurun (yoksa `GitKur.bat`)
3. **`IndirVeKur.bat`** çalıştırın — GitHub'dan indirir, paketleri yükler, derler
4. Program: **`Calistir.bat`** veya `bin\Debug\net8.0-windows\PhonixFrame.exe`
5. Masaüstü kısayolu: **`MasaustuKur.bat`** (projeden bağımsız açmak için)

Depo zaten indirildiyse proje klasöründe **`Kur.bat`** yeterlidir.

**Varsayılan logo assetleri** (`Assets/` içinde, repoda dahil):

| Dosya | Açıklama |
|--------|----------|
| `filigram-08.svg` | Dikey beyaz marka logosu |
| `filigram-09.svg` | Dikey siyah marka logosu |
| `nadir-figur-yatay-beyaz.svg` | Yatay beyaz logo |
| `nadir-figur-yatay-siyah.svg` | Yatay siyah logo |

Derleme hatası alırsanız sırayla deneyin: **`Derle.bat`** → **`TamTemizlik.bat`** → **`TemizleVeCalistir.bat`**. Ayrıntılar `build-output.txt` dosyasına yazılır.

**Giriş şifresi:** uygulama içinde tanımlı (varsayılan kurulum).

## Özellikler (son sürüm)

### Görsel işleme
- **20+ şablon** (beyaz stüdyo, Pinterest, Story 9:16, Polaroid, Trendyol kare, lüks çerçeve, banner şerit vb.)
- **17 renk paketi** + özel zemin / RONEKAI / .DEN (hex veya RGB)
- **Kayıtlı profiller** — şablon, renk, logo, çıktı ve gelişmiş ayarları tek tıkla yükle
- **Favori / son kullanılan şablonlar**
- **Görseldeki marka metni** özelleştirilebilir (ana metin + ek, göster/gizle, boyut %)
- **Responsif sığdır** — şablon alanını boşluk bırakmadan doldurma (isteğe bağlı)
- **Ek metin katmanı** (fiyat, SKU, kampanya)
- **7 logo modu** + varsayılan veya özel logo dosyası
- **Sadece boyutlandır** modu (şablon olmadan ölçekleme)
- **JPEG kalitesi** ayarı ve **PNG çıktı** seçeneği
- **Dosya adı** — varsayılan orijinal ad (`IMG_001.jpg`); tarih çıktı klasör adında

### Canlı önizleme
- Kaynak klasördeki **gerçek fotoğraf** ile canlı önizleme (yoksa demo)
- **Dosya listesinden seçilen** görsel önizlemede gösterilir
- Yanında **işlem günlüğü**

### Logo ve marka
- **Marka logosu** — konum, boyut, opaklık, X/Y ofset, renk/gradyan tint
- **Filigran logosu** — 9 konum + ölçek %
- **SVG logo** desteği (rasterize edilir)

### Platform / çıktı boyutu
- Şablon boyutu, kaynak dosya boyutu
- Instagram, WhatsApp, Sahibinden, Facebook, LinkedIn, Google Merchant, e-ticaret pro, web optimize, Amazon vb.

### Kaynak ve çıktı
- **JPG/JFIF, PNG, WEBP, AVIF, BMP, GIF, TIFF, ICO, SVG, HEIC/HEIF, .hdc
- JPEG okuma: ImageSharp başarısız olursa **Windows WPF yedek kodlayıcı**
- Bozuk veya bulut önizleme dosyaları için **açıklayıcı hata mesajları**
- **Sürükle-bırak** klasör veya dosya; **dosya listesinden** yalnızca seçilenleri işleme
- İşlem öncesi **örnek önizleme** (`_Onizleme_Ornekleri` alt klasörü)
- Çıktı: kaynak klasör içinde `PhonixFrame_yyyy-MM-dd_HHmmss/`
- Çıktı: **JPEG** (ayarlanabilir kalite) veya **PNG**

### Arayüz
- Üstte geniş **önizleme + günlük**, altta iki sütun **ayarlar**
- Başlık markası (logo / metin) özelleştirilebilir

## Logo dosyası

**Programla gelen marka logoları** (`Assets/` — GitHub'da dahil):

- `filigram-08.svg`, `filigram-09.svg` (dikey beyaz/siyah)
- `nadir-figur-yatay-beyaz.svg`, `nadir-figur-yatay-siyah.svg` (yatay)

İsteğe bağlı filigran logosu:

```
Assets/ronekai-logo.png
```

(veya `logo.png`, `ronekai-logo.jpg`, `*.svg`)

## Mac / iPhone HEIC

HEIC okumak için Windows'ta [HEIF Image Extensions](https://apps.microsoft.com/detail/9n4wgh0z6vhq) (ücretsiz) gerekebilir.

## Betikler (.bat)

| Dosya | Açıklama |
|--------|----------|
| **`IndirVeKur.bat`** | **GitHub'dan indir + ilk kurulum** (yeni bilgisayar) |
| **`Kur.bat`** | Paketleri yükle, asset kontrolü, derle |
| **`MasaustuKur.bat`** | **Masaüstüne kur** — Release derle, kısayol oluştur |
| `Calistir.bat` | Derle ve programı aç |
| `Derle.bat` | Sadece derle |
| `TamTemizlik.bat` | bin/obj sil + derle |
| `TemizleVeCalistir.bat` | Tam temizlik + çalıştır |
| `YenidenDerle.bat` | Hızlı yeniden derleme |
| `GitHubYukle.bat` | GitHub'a commit + push |
| `GitKur.bat` | Git / gh kurulum yardımcısı |

Ortak derleme: `_BuildCommon.bat` (doğrudan çalıştırmayın).

Commit mesajı: `commit-msg.txt` · Derleme logu: `build-output.txt` · GitHub logu: `github-yukle-log.txt`

## GitHub'a yükleme

Depo: https://github.com/ronekai/RonekaiFrame

1. `gh auth login` (bir kez; Git yoksa `GitKur.bat`)

2. `commit-msg.txt` güncelleyin (isteğe bağlı)

3. **`GitHubYukle.bat`** — isteğe bağlı derleme, sonra push

**Dal:** `main` (varsayılan)

## Proje yapısı

| Klasör | Açıklama |
|--------|----------|
| `Templates/` | Görsel şablonları |
| `Services/` | Toplu işlem, önizleme, logo, HEIC, WPF yedek JPEG okuyucu |
| `Controls/` | Başlık markası bileşenleri |
| `Assets/` | Varsayılan marka logoları (SVG) + isteğe bağlı filigran |

## Gereksinimler

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git + [GitHub CLI](https://cli.github.com/) (yükleme için)

## Lisans

MIT
