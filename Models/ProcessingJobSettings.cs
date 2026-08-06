using System.Collections.Generic;

namespace RonekaiImageFramer.Models;

public sealed class ProcessingJobSettings
{
    public bool ResizeOnly { get; init; }
    public bool StretchToExport { get; init; }
    public bool ResponsiveProductFit { get; init; }
    public int JpegQuality { get; init; } = 92;
    public bool SaveAsPng { get; init; }
    public string FileNamePattern { get; init; } = "{base}";
    public TextOverlaySettings TextOverlay { get; init; } = new();
    public int SamplePreviewCount { get; init; }
    public bool ProcessOnlySelectedFiles { get; init; }
    public NormalizedCropRect? CropRect { get; init; }
    public bool CropOnlySelectedFiles { get; init; }
    public IReadOnlyList<string> CropSelectedFilePaths { get; init; } = [];

    /// <summary>Uygulanacak filigram temizleme işlemleri (sırayla) — aktif önizleme dosyası.</summary>
    public IReadOnlyList<WatermarkCleanOp> WatermarkCleanOps { get; init; } = [];

    /// <summary>Klon damga (doku transferi) işlemleri — temizlikten sonra, logodan önce.</summary>
    public IReadOnlyList<TextureCloneOp> TextureCloneOps { get; init; } = [];

    /// <summary>Şekil seçim kopyala-yapıştır (döndürmeli) işlemleri.</summary>
    public IReadOnlyList<SelectionPasteOp> SelectionPasteOps { get; init; } = [];

    /// <summary>Dosya bazlı filigram işlemleri (çoklu seçimde her dosya kendi işini alır).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<WatermarkCleanOp>> WatermarkCleanOpsByFile { get; init; }
        = new Dictionary<string, IReadOnlyList<WatermarkCleanOp>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Dosya bazlı klon işlemleri.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<TextureCloneOp>> TextureCloneOpsByFile { get; init; }
        = new Dictionary<string, IReadOnlyList<TextureCloneOp>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Dosya bazlı yapıştırma işlemleri.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<SelectionPasteOp>> SelectionPasteOpsByFile { get; init; }
        = new Dictionary<string, IReadOnlyList<SelectionPasteOp>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Dosya bazlı kırpma (normalize).</summary>
    public IReadOnlyDictionary<string, NormalizedCropRect> CropRectByFile { get; init; }
        = new Dictionary<string, NormalizedCropRect>(StringComparer.OrdinalIgnoreCase);

    public static ProcessingJobSettings Default => new();

    public IReadOnlyList<WatermarkCleanOp> ResolveWatermarkCleanOps(string? sourceFile)
    {
        if (TryGetByFile(WatermarkCleanOpsByFile, sourceFile, out var ops))
            return ops;
        if (WatermarkCleanOpsByFile.Count > 0)
            return [];
        return WatermarkCleanOps;
    }

    public IReadOnlyList<TextureCloneOp> ResolveTextureCloneOps(string? sourceFile)
    {
        if (TryGetByFile(TextureCloneOpsByFile, sourceFile, out var ops))
            return ops;
        if (TextureCloneOpsByFile.Count > 0)
            return [];
        return TextureCloneOps;
    }

    public IReadOnlyList<SelectionPasteOp> ResolveSelectionPasteOps(string? sourceFile)
    {
        if (TryGetByFile(SelectionPasteOpsByFile, sourceFile, out var ops))
            return ops;
        if (SelectionPasteOpsByFile.Count > 0)
            return [];
        return SelectionPasteOps;
    }

    public NormalizedCropRect? ResolveCropRect(string? sourceFile, NormalizedCropRect? cropOverride = null)
    {
        if (TryGetByFile(CropRectByFile, sourceFile, out var crop))
            return crop;
        return cropOverride ?? CropRect;
    }

    public bool HasDeferredBrandOps(string? sourceFile, NormalizedCropRect? cropOverride = null)
    {
        return ResolveCropRect(sourceFile, cropOverride) is not null
               || ResolveWatermarkCleanOps(sourceFile).Count > 0
               || ResolveTextureCloneOps(sourceFile).Count > 0
               || ResolveSelectionPasteOps(sourceFile).Count > 0;
    }

    private static bool TryGetByFile<T>(
        IReadOnlyDictionary<string, T> map,
        string? sourceFile,
        out T value)
    {
        value = default!;
        if (map is null || map.Count == 0 || string.IsNullOrWhiteSpace(sourceFile))
            return false;

        if (map.TryGetValue(sourceFile, out value!))
            return true;

        string full;
        try
        {
            full = Path.GetFullPath(sourceFile);
        }
        catch
        {
            return false;
        }

        if (map.TryGetValue(full, out value!))
            return true;

        foreach (var kv in map)
        {
            if (string.Equals(kv.Key, sourceFile, StringComparison.OrdinalIgnoreCase)
                || string.Equals(kv.Key, full, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }

            try
            {
                if (string.Equals(Path.GetFullPath(kv.Key), full, StringComparison.OrdinalIgnoreCase))
                {
                    value = kv.Value;
                    return true;
                }
            }
            catch
            {
                // ignore bad keys
            }
        }

        return false;
    }
}
