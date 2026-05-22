using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class BrandFontRegistry
{
    private static readonly string WinFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    public static IReadOnlyList<BrandFontOption> All { get; } =
    [
        Font("segoe-ui", "Segoe UI", "segoeuib.ttf", "segoeui.ttf"),
        Font("arial", "Arial", "arialbd.ttf", "arial.ttf"),
        Font("calibri", "Calibri", "calibrib.ttf", "calibri.ttf"),
        Font("times", "Times New Roman", "timesbd.ttf", "times.ttf"),
        Font("georgia", "Georgia", "georgiab.ttf", "georgia.ttf"),
        Font("verdana", "Verdana", "verdanab.ttf", "verdana.ttf"),
        Font("tahoma", "Tahoma", "tahomabd.ttf", "tahoma.ttf"),
        Font("trebuchet", "Trebuchet MS", "trebucbd.ttf", "trebuc.ttf"),
        Font("consolas", "Consolas", "consolab.ttf", "consola.ttf"),
        Font("corbel", "Corbel", "corbelb.ttf", "corbel.ttf"),
        Font("cambria", "Cambria", "cambriab.ttf", "cambria.ttf"),
        Font("impact", "Impact", "impact.ttf", "impact.ttf"),
    ];

    public static BrandFontOption Default => All[0];

    public static BrandFontOption? GetById(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : All.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private static BrandFontOption Font(string id, string name, string boldFile, string regularFile) =>
        new()
        {
            Id = id,
            Name = name,
            BoldFontPath = Path.Combine(WinFonts, boldFile),
            RegularFontPath = Path.Combine(WinFonts, regularFile)
        };
}
