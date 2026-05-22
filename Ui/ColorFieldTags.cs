namespace RonekaiImageFramer.Ui;

/// <summary>Renk alanı TextBox / damla düğmesi Tag değerleri.</summary>
public static class ColorFieldTags
{
    public const string Background = "Background";
    public const string MainText = "MainText";
    public const string Suffix = "Suffix";

    public static bool IsMainText(string? tag) =>
        string.Equals(tag, MainText, StringComparison.OrdinalIgnoreCase)
        || string.Equals(tag, "Ronekai", StringComparison.OrdinalIgnoreCase);

    public static bool IsSuffix(string? tag) =>
        string.Equals(tag, Suffix, StringComparison.OrdinalIgnoreCase)
        || string.Equals(tag, "Den", StringComparison.OrdinalIgnoreCase);
}
