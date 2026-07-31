using HeyRed.ImageSharp.Heif.Formats.Avif;
using HeyRed.ImageSharp.Heif.Formats.Heif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RonekaiImageFramer.Services;

/// <summary>
/// HEIC/HEIF/AVIF okuyucu.
/// Once libheif (HeyRed.ImageSharp.Heif), basarisizsa Windows WPF kodlayici.
/// </summary>
public static class HeifDecoder
{
    public const string InstallHint =
        "AVIF/HEIC okunamadi.\n\n" +
        "1) Programla gelen libheif kutuphanesi calisiyor olmali.\n" +
        "2) Alternatif: Microsoft Store -> HEIF Image Extensions + AV1 Video Extension.\n" +
        "3) Dosyayi PNG/JPEG olarak disa aktarip tekrar deneyin.";

    private static readonly Lazy<Configuration> LibHeifConfig = new(CreateLibHeifConfiguration);

    private static Configuration CreateLibHeifConfiguration() =>
        new(new AvifConfigurationModule(), new HeifConfigurationModule());

    public static Image<Rgba32> Load(string filePath)
    {
        Exception? libHeifEx = null;
        try
        {
            return LoadWithLibHeif(filePath);
        }
        catch (Exception ex)
        {
            libHeifEx = ex;
        }

        try
        {
            return WpfBitmapDecoder.Load(filePath);
        }
        catch (Exception wpfEx)
        {
            throw new InvalidOperationException(
                $"AVIF/HEIC/HEIF okunamadi: {Path.GetFileName(filePath)}.\n\n{InstallHint}",
                new AggregateException(libHeifEx!, wpfEx));
        }
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
}