using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgColor = SixLabors.ImageSharp.Color;

namespace RonekaiImageFramer.Services;

/// <summary>Canlı önizleme için örnek ürün görseli (gerçek fotoğraf gerekmez).</summary>
public static class DemoProductImage
{
    private static Image<Rgba32>? _cached;

    public static Image<Rgba32> Create()
    {
        if (_cached != null)
            return _cached.CloneAs<Rgba32>();

        const int size = 900;
        var img = new Image<Rgba32>(size, size);
        img.Mutate(ctx =>
        {
            ctx.Fill(ImgColor.ParseHex("#E8ECF2"));
            var card = new RectangleF(size * 0.12f, size * 0.1f, size * 0.76f, size * 0.8f);
            ctx.Fill(ImgColor.White, card);
            ctx.Draw(ImgColor.ParseHex("#D0D5DE"), 3, card);

            var inner = new RectangleF(card.X + 40, card.Y + 50, card.Width - 80, card.Height - 120);
            ctx.Fill(ImgColor.ParseHex("#C5D0E0"), inner);

            var highlight = new RectangleF(inner.X + 30, inner.Y + 30, inner.Width * 0.55f, inner.Height * 0.5f);
            ctx.Fill(ImgColor.ParseHex("#9AABC4"), highlight);
        });

        _cached = img.CloneAs<Rgba32>();
        return img;
    }
}
