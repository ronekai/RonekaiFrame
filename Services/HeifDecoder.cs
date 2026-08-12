using HeyRed.ImageSharp.Heif.Formats.Avif;
using HeyRed.ImageSharp.Heif.Formats.Heif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RonekaiImageFramer.Services;

/// <summary>
/// HEIC/HEIF/AVIF okuyucu.
/// AVIF: Windows WIC (Microsoft HEIF Decoder) önce — libheif/aom bazı AVIF'leri
/// neredeyse siyah decode edebiliyor. HEIC: libheif önce, gerekirse WIC.
/// </summary>
public static class HeifDecoder
{
    public const string InstallHint =
        "AVIF/HEIC okunamadi.\n\n" +
        "1) Microsoft Store → HEIF Image Extensions + AV1 Video Extension kurun.\n" +
        "2) Programla gelen libheif kutuphanesi yedek olarak denenir.\n" +
        "3) Dosyayi PNG/JPEG olarak disa aktarip tekrar deneyin.";

    private static readonly Lazy<Configuration> LibHeifConfig = new(CreateLibHeifConfiguration);

    private static Configuration CreateLibHeifConfiguration() =>
        new(new AvifConfigurationModule(), new HeifConfigurationModule());

    public static Image<Rgba32> Load(string filePath)
    {
        bool preferWpf = ImageInputCatalog.LooksLikeAvif(filePath)
                         || ImageInputCatalog.IsAvifFile(filePath);

        Exception? firstEx = null;

        if (preferWpf)
        {
            try
            {
                return LoadWithWpf(filePath);
            }
            catch (Exception ex)
            {
                firstEx = ex;
            }

            try
            {
                return LoadWithLibHeif(filePath);
            }
            catch (Exception libEx)
            {
                throw new InvalidOperationException(
                    $"AVIF/HEIC/HEIF okunamadi: {Path.GetFileName(filePath)}.\n\n{InstallHint}",
                    new AggregateException(firstEx!, libEx));
            }
        }

        // HEIC / genel HEIF: libheif önce; siyah/bozuksa WIC'e düş
        Image<Rgba32>? libImg = null;
        try
        {
            libImg = LoadWithLibHeif(filePath);
            if (!IsNearBlackDecode(libImg))
                return libImg;

            try
            {
                var wpfImg = LoadWithWpf(filePath);
                if (!IsNearBlackDecode(wpfImg) || MaxChannel(wpfImg) > MaxChannel(libImg))
                {
                    libImg.Dispose();
                    return wpfImg;
                }

                wpfImg.Dispose();
                return libImg;
            }
            catch
            {
                return libImg;
            }
        }
        catch (Exception libEx)
        {
            libImg?.Dispose();
            firstEx = libEx;
        }

        try
        {
            return LoadWithWpf(filePath);
        }
        catch (Exception wpfEx)
        {
            throw new InvalidOperationException(
                $"AVIF/HEIC/HEIF okunamadi: {Path.GetFileName(filePath)}.\n\n{InstallHint}",
                new AggregateException(firstEx!, wpfEx));
        }
    }

    private static Image<Rgba32> LoadWithWpf(string filePath)
    {
        var image = WpfBitmapDecoder.Load(filePath);
        image.Mutate(ctx => ctx.AutoOrient());
        return image;
    }

    private static Image<Rgba32> LoadWithLibHeif(string filePath)
    {
        var options = new DecoderOptions
        {
            Configuration = LibHeifConfig.Value
        };
        var image = Image.Load<Rgba32>(options, filePath);
        image.Mutate(ctx => ctx.AutoOrient());
        return image;
    }

    /// <summary>
    /// libheif/aom bazen geçerli boyutlu ama neredeyse tamamen siyah kare üretir.
    /// avgRGB &lt; 2 ve max kanal &lt; 24 → bozuk decode kabul et.
    /// </summary>
    private static bool IsNearBlackDecode(Image<Rgba32> image)
    {
        if (image.Width < 2 || image.Height < 2)
            return true;

        byte max = 0;
        long sum = 0;
        long count = 0;
        // Performans: her 4. piksel
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < image.Height; y += 2)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x += 2)
                {
                    var p = row[x];
                    max = Math.Max(max, Math.Max(p.R, Math.Max(p.G, p.B)));
                    sum += p.R + p.G + p.B;
                    count++;
                }
            }
        });

        if (count == 0)
            return true;

        double avg = sum / (double)(count * 3);
        return max < 24 && avg < 2.0;
    }

    private static byte MaxChannel(Image<Rgba32> image)
    {
        byte max = 0;
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < image.Height; y += 4)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x += 4)
                {
                    var p = row[x];
                    max = Math.Max(max, Math.Max(p.R, Math.Max(p.G, p.B)));
                }
            }
        });
        return max;
    }
}
