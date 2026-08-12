# Değişiklik günlüğü

## PhonixFrame — 2026-08-12

### Pin klon damgası (kaynak al)
- **Kaynak al** anında seçim, görünen önizlemeden PNG olarak kilitlenir; damga bire bir aynı pikselleri yapıştırır
- 5+ pinde convex hull kaldırıldı — tıklama sırası korunur (altıgen / çokgen seçim küçülmez)
- Çokgen maskesi ImageSharp vektör dolgu ile uygulanır (eksik alt/üst kesit sorunu giderildi)
- Klon logo/metin **sonrasında** uygulanır (önizleme ile dışa aktarım aynı sıra)
- Damga pivotu pin centroid’ine hizalanır; döndürme yokken tam yama ROI’si kullanılır
- Önizleme piksel boyutu ile bake ölçeği hizalandı

---

## PhonixFrame — 2026-08-09

### Şablonlar
- Liste sadeleştirildi: **Shopier (1:1)**, **Web (4:3)**, **Instagram (4:5)** — her biri **Beyaz / Siyah**
- Şablon isimlerinde oran etiketi (ör. `1:1 · akıllı boyut`)
- Zemin artık sabit S/B değil; **renk paleti** (paket / damla / gradyan) ile doldurulur
- **Akıllı boyut:** büyük kaynakta seçilen orana en yakın 100 px’lik tuval  
  (ör. `2384×2200` → Shopier `2400×2400`, Web `3000×2250`, Instagram `2400×3000`)

### Kenar uzatma (letterbox)
- **Kenarları uzat:** şablonun sol/sağ veya üst/alt boşluğunu fotoğraf kenar tonuyla doldurur  
  (saf beyaz palet ile gri stüdyo zemini farkını kapatır)
- İnce beyaz birleşim çizgisi giderildi (taşırma + tam dolgu)
- **Şeridi kenara uzat:** dikey seçim → sol/sağ; yatay seçim → üst/alt
- İşlem sırası: şablon → kenar uzat → klon → **marka/logo** (logo uzatılan zeminin üstüne oturur)

### Klon / doku
- Klon damgası şablon tuvali uzayında; tamamlanan (uzatılan) alanlara da uygulanır

### Notlar
- Eski stüdyo / Pinterest / Story vb. şablonlar listeden kaldırıldı (`Şablon yok` / `Yay` duruyor)
- Masaüstü kurulum: `MasaustuKur.bat` veya `%LOCALAPPDATA%\PhonixFrame`

---

## PhonixFrame v1.0 (2026-05)

### Yeniden markalama
- Uygulama adı **PhonixFrame** (`PhonixFrame.exe`)
- Çıktı klasörleri: `PhonixFrame_yyyy-MM-dd_HHmmss`

### Arayüz
- Üst bölüm: geniş **canlı önizleme** + **işlem günlüğü**
- Alt bölüm: iki sütunlu ayarlar (şablon/renk/marka | logo/klasör)
- Platform çözünürlük seçici (Instagram, WhatsApp, Sahibinden, vb.)

### Şablonlar ve işleme
- 12 şablon, şablon listesinde px boyutları
- Özelleştirilebilir görsel marka metni
- Çıktı ölçekleme: şablon, kaynak boyutu, sabit platform boyutları

### Logo
- Varsayılan logo / özel logo seçimi
- PNG, JPEG; HEIC ve diğer formatlar JPEG önbelleğe dönüştürülür
- Format bilgisi arayüzde gösterilir

### Diğer
- Giriş ekranı
- Mac HEIC/HEIF kaynak desteği
- Toplu işlem, alt klasör tarama
