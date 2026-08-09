using System.Globalization;
using RonekaiImageFramer.Templates;

namespace RonekaiImageFramer.Models;

public sealed class TemplateListItem(IProductTemplate template)
{
    public IProductTemplate Template { get; } = template;

    public string Name => Template.IsPassthrough
        ? (Template.StretchToExport ? $"{Template.Name} (çıktı boyutuna yay)" : Template.Name)
        : Template.UsesSmartOutputSize
            ? $"{Template.Name} ({FormatAspectRatio(Template.OutputSize.Width, Template.OutputSize.Height)} · akıllı boyut)"
            : $"{Template.Name} ({FormatAspectRatio(Template.OutputSize.Width, Template.OutputSize.Height)} · {Template.OutputSize.Width}×{Template.OutputSize.Height} px)";

    public string SizeLabel => Template.UsesSmartOutputSize
        ? $"Akıllı · min {Template.OutputSize.Width} × {Template.OutputSize.Height} px"
        : $"{Template.OutputSize.Width} × {Template.OutputSize.Height} px";

    public string Description => Template.Description;

    /// <summary>GCD ile sadeleştirilmiş oran (ör. 4:3, 9:16); çok büyük sayılarda ondalık.</summary>
    public static string FormatAspectRatio(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return "?";

        int g = Gcd(width, height);
        int rw = width / g;
        int rh = height / g;

        if (rw <= 21 && rh <= 21)
            return $"{rw}:{rh}";

        double r = (double)width / height;
        return r >= 1
            ? $"{r.ToString("0.##", CultureInfo.InvariantCulture)}:1"
            : $"1:{(1.0 / r).ToString("0.##", CultureInfo.InvariantCulture)}";
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            int t = a % b;
            a = b;
            b = t;
        }
        return Math.Abs(a);
    }
}
