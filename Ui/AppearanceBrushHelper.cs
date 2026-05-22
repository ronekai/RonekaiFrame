using System.Windows.Media;
using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Ui;

public static class AppearanceBrushHelper
{
    public static Brush ToPreviewBrush(ThemeColorAppearance appearance)
    {
        try
        {
            if (appearance.FillMode == ColorFillMode.Gradient
                && UiColorHelper.TryParseHex(appearance.PrimaryHex, out var start)
                && UiColorHelper.TryParseHex(appearance.GradientEndHex, out var end))
            {
                var brush = new LinearGradientBrush
                {
                    StartPoint = appearance.GradientDirection switch
                    {
                        GradientDirection.Horizontal => new System.Windows.Point(0, 0.5),
                        GradientDirection.DiagonalDown => new System.Windows.Point(0, 0),
                        GradientDirection.DiagonalUp => new System.Windows.Point(0, 1),
                        _ => new System.Windows.Point(0.5, 0)
                    },
                    EndPoint = appearance.GradientDirection switch
                    {
                        GradientDirection.Horizontal => new System.Windows.Point(1, 0.5),
                        GradientDirection.DiagonalDown => new System.Windows.Point(1, 1),
                        GradientDirection.DiagonalUp => new System.Windows.Point(1, 0),
                        _ => new System.Windows.Point(0.5, 1)
                    },
                    Opacity = Math.Clamp(appearance.Opacity, 0.05, 1)
                };
                brush.GradientStops.Add(new GradientStop(UiColorHelper.ParseWpfColor(start), 0));
                brush.GradientStops.Add(new GradientStop(UiColorHelper.ParseWpfColor(end), 1));
                brush.Freeze();
                return brush;
            }

            var solid = UiColorHelper.ToSolidBrush(appearance.PrimaryHex);
            solid.Opacity = Math.Clamp(appearance.Opacity, 0.05, 1);
            solid.Freeze();
            return solid;
        }
        catch
        {
            return Brushes.LightGray;
        }
    }
}
