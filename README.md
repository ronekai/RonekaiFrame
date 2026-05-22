# PhonixFrame

**PhonixFrame** — Windows (.NET 8 WPF) masaüstü uygulaması. E-ticaret ve ilan görsellerinizi toplu olarak şablonlara oturtur; canlı önizleme, platform çözünürlükleri ve logo yönetimi içerir.

> Önceki ad: RonekaiFrame. GitHub deposu `RonekaiFrame` altında kalabilir; uygulama adı ve exe artık **PhonixFrame**'dir.

## Hızlı başlangıç

```powershell
cd "$env:USERPROFILE\Source\Repos\RonekaiFrame"
taskkill /IM PhonixFrame.exe /F 2>$null
taskkill /IM RonekaiFrame.exe /F 2>$null
dotnet build
dotnet run
```

veya **`Calistir.bat`** / **`Derle.bat`** dosyalarına çift tıklayın.

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
- **Özel dosya adı şablonu** (`{base}`, `{stamp}`, `{template}`, `{export}` …)

### Canlı önizleme
- Kaynak klasördeki **ilk gerçek fotoğraf** ile canlı önizleme (yoksa demo)
- Yanında **işlem günlüğü**

### Platform / çıktı boyutu
- Şablon boyutu, kaynak dosya boyutu
- Instagram, WhatsApp, Sahibinden, Facebook, LinkedIn, Google Merchant, e-ticaret pro, web optimize, Amazon vb.

### Kaynak ve çıktı
- **JPG, PNG, WEBP, HEIC/HEIF** (alt klasörler dahil)
- **Sürükle-bırak** klasör veya dosya; **dosya listesinden** yalnızca seçilenleri işleme
- İşlem öncesi **örnek önizleme** (`_Onizleme_Ornekleri` alt klasörü)
- Çıktı: kaynak klasör içinde `PhonixFrame_yyyy-MM-dd_HHmmss/`
- Çıktı: **JPEG** (ayarlanabilir kalite) veya **PNG**

### Arayüz
- Üstte geniş **önizleme + günlük**, altta iki sütun **ayarlar**
- Başlık markası (logo / metin) özelleştirilebilir

## Logo dosyası

```
Assets/ronekai-logo.png
```

(veya `logo.png`, `ronekai-logo.jpg` — Assets klasöründe)

## Mac / iPhone HEIC

HEIC okumak için Windows'ta [HEIF Image Extensions](https://apps.microsoft.com/detail/9n4wgh0z6vhq) (ücretsiz) gerekebilir.

## GitHub'a yükleme (tek sürüm)

Depo: https://github.com/ronekai/RonekaiFrame

**Eski GitHub geçmişini silip yalnızca güncel PhonixFrame kodunu bırakmak için:**

`GitHubTekSurum.bat` dosyasına çift tıklayın → onaylayın → `force push` ile tek commit kalır.

Gereksinim: `gh auth login` (hesap: ronekai).

**Dal:** Yalnızca **`main`** kullanın. Eski **`master`** zorunlu değildir; GitHub’da iki dal görünüyorsa `master`’ı silebilirsiniz (Settings → Default branch: **main**).

Diğer betikler:
- `GitHubYukle-Manuel.bat` — normal push (geçmişi korur)
- `GitHubYukle.bat` — PowerShell betiği (Bypass ile)

## Proje yapısı

| Klasör | Açıklama |
|--------|----------|
| `Templates/` | Görsel şablonları |
| `Services/` | Toplu işlem, önizleme, logo, HEIC |
| `Controls/` | Başlık markası bileşenleri |
| `Assets/` | Varsayılan logo |

## Gereksinimler

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git + [GitHub CLI](https://cli.github.com/) (yükleme için)

## Lisans

MIT
