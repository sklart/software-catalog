using System.Text;
using System.Text.RegularExpressions;

namespace SoftwareCatalog.Core;

public sealed class ProductNormalizer
{
    private static readonly Regex Suffix = new(@"\b(setup|installer|installation|install|x64|x86|arm64)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase) { ["mozilla firefox"] = "firefox", ["7 zip"] = "7zip", ["7zip"] = "7zip", ["notepad"] = "notepad++" };
    public string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var text = value.Trim().ToLowerInvariant().Replace("-", " ");
        text = Suffix.Replace(text, " ");
        var builder = new StringBuilder();
        foreach (var c in text) if (char.IsLetterOrDigit(c) || c == '+') builder.Append(c); else builder.Append(' ');
        text = Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
        return Aliases.TryGetValue(text, out var alias) ? alias : text.Replace(" ", string.Empty);
    }
    public string? NormalizeVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim().TrimStart('v', 'V');
        return text.All(c => char.IsDigit(c) || c == '.') ? text : null;
    }
}
