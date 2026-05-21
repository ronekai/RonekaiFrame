using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RonekaiImageFramer.Services;

/// <summary>Kaynak dosyayı ImageSharp <see cref="Image{Rgba32}"/> olarak yükler (HEIC/HEIF dahil).</summary>
public static class SourceImageLoader
{
    public static Image<Rgba32> Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Dosya bulunamadı.", filePath);

        if (ImageInputCatalog.IsHeifFile(filePath))
            return HeifDecoder.Load(filePath);

        var image = Image.Load<Rgba32>(filePath);
        image.Mutate(ctx => ctx.AutoOrient());
        return image;
    }
}
