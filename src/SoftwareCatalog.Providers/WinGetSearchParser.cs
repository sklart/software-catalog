using System.Text.RegularExpressions;

namespace SoftwareCatalog.Providers;

public static partial class WinGetSearchParser
{
    [GeneratedRegex(@"^(?<name>.+?)(?:\s{2,})(?<id>[A-Za-z0-9][A-Za-z0-9._-]*)(?:\s{2,})(?<version>\S+)\s*$")]
    private static partial Regex Row();
    public static IReadOnlyList<WinGetPackage> Parse(string output) => output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => Row().Match(line.TrimEnd())).Where(match => match.Success).Select(match => new WinGetPackage(match.Groups["id"].Value, match.Groups["name"].Value.Trim(), null, match.Groups["version"].Value)).ToArray();
}
