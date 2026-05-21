using SixLabors.Fonts;
using SixLaborsFontFamily = SixLabors.Fonts.FontFamily;

namespace RonekaiImageFramer.Services;

public static class FontProvider
{
    private static readonly string[] BoldCandidates =
    [
        @"C:\Windows\Fonts\segoeuib.ttf",
        @"C:\Windows\Fonts\arialbd.ttf",
        @"C:\Windows\Fonts\calibrib.ttf",
    ];

    private static readonly string[] RegularCandidates =
    [
        @"C:\Windows\Fonts\segoeui.ttf",
        @"C:\Windows\Fonts\arial.ttf",
        @"C:\Windows\Fonts\calibri.ttf",
    ];

    private static SixLaborsFontFamily? _bold;
    private static SixLaborsFontFamily? _regular;

    public static SixLaborsFontFamily GetBoldFamily() =>
        _bold ??= LoadFamily(BoldCandidates) ?? SystemFonts.Families.First();

    public static SixLaborsFontFamily GetRegularFamily() =>
        _regular ??= LoadFamily(RegularCandidates) ?? SystemFonts.Families.First();

    private static SixLaborsFontFamily? LoadFamily(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            try
            {
                return new FontCollection().Add(path);
            }
            catch
            {
                // sonraki font
            }
        }

        return null;
    }
}
