using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;

public sealed class VersionComparer
{
    public VersionComparisonResult Compare(string? local, string? remote)
    {
        if (!TryParse(local, out var left) || !TryParse(remote, out var right)) return VersionComparisonResult.Unknown;
        var length = Math.Max(left.Length, right.Length);
        for (var i = 0; i < length; i++) { var a = i < left.Length ? left[i] : 0; var b = i < right.Length ? right[i] : 0; if (a < b) return VersionComparisonResult.Older; if (a > b) return VersionComparisonResult.Newer; }
        return VersionComparisonResult.Equal;
    }
    public bool TryParse(string? text, out int[] values)
    {
        values = []; if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim().TrimStart('v', 'V'); var parts = text.Split('.', StringSplitOptions.None);
        if (parts.Length == 0 || parts.Length > 8) return false;
        foreach (var part in parts) if (!int.TryParse(part, out var value) || value < 0) return false; else values = [.. values, value];
        return true;
    }
}
