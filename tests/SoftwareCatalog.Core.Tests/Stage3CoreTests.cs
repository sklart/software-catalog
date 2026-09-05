using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Tests;

public sealed class Stage3CoreTests
{
    [Theory]
    [InlineData("7-Zip", "7zip")]
    [InlineData("Mozilla Firefox", "Firefox Setup")]
    [InlineData("Notepad++ Installer", "Notepad++")]
    public void NormalizerGroupsKnownEquivalentNames(string left, string right) => Assert.Equal(new ProductNormalizer().Normalize(left), new ProductNormalizer().Normalize(right));
    [Fact] public void NormalizerKeepsDistinctProductsApart() { var n = new ProductNormalizer(); Assert.NotEqual(n.Normalize("Visual Studio"), n.Normalize("Visual Studio Code")); Assert.NotEqual(n.Normalize("Python"), n.Normalize("Python Launcher")); }
    [Theory]
    [InlineData("1.2", "1.3", VersionComparisonResult.Older)]
    [InlineData("1.2.9", "1.2.10", VersionComparisonResult.Older)]
    [InlineData("1.2.3", "1.2.3.0", VersionComparisonResult.Equal)]
    [InlineData("25.00", "26.00", VersionComparisonResult.Older)]
    [InlineData("bad", "1.0", VersionComparisonResult.Unknown)]
    public void ComparesVersions(string local, string remote, VersionComparisonResult expected) => Assert.Equal(expected, new VersionComparer().Compare(local, remote));
}
