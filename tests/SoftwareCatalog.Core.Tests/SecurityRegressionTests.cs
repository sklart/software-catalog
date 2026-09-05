namespace SoftwareCatalog.Core.Tests;

public sealed class SecurityRegressionTests
{
    [Fact]
    public void StageFourNeverInstallsUpgradesOrExecutesDownloadedInstaller()
    {
        var root = Ancestors(AppContext.BaseDirectory).First(path => Directory.Exists(Path.Combine(path, "src")));
        var winGet = File.ReadAllText(Path.Combine(root, "src", "SoftwareCatalog.Providers", "WinGetProvider.cs"));
        var ui = File.ReadAllText(Path.Combine(root, "src", "SoftwareCatalog.UI", "ViewModels", "MainViewModel.cs"));
        Assert.DoesNotContain("winget install", winGet, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("winget upgrade", winGet, StringComparison.OrdinalIgnoreCase);
        var starts = ui.Split('\n').Where(line => line.Contains("Process.Start", StringComparison.Ordinal)).ToArray(); Assert.All(starts, line => Assert.Contains("explorer.exe", line, StringComparison.OrdinalIgnoreCase));
    }
    private static IEnumerable<string> Ancestors(string path) { var current = new DirectoryInfo(path); while (current is not null) { yield return current.FullName; current = current.Parent; } }
}
