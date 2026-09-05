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
    [Fact]
    public void MatchingPrefersStableIdentifiersAndPublisherAwareNames()
    {
        var now = DateTimeOffset.UtcNow; var id = Guid.NewGuid(); var matcher = new ProductMatchingService(new ProductNormalizer());
        var msi = new InstallerFile(1, 1, "a.msi", "a.msi", ".msi", 1, now, null, now, now, true, ProductName: "Tool", Publisher: "Same", UpgradeCode: "upgrade", ProductId: id);
        var sameUpgrade = msi with { Id = 2, UpgradeCode = "upgrade", ProductId = null, Publisher = "Other" };
        Assert.Equal(ProductMatchSource.MsiUpgradeCode, matcher.FindMatch(sameUpgrade, [msi])!.Source);
        var sameName = msi with { Id = 3, UpgradeCode = null, ProductCode = null, ProductId = null };
        Assert.Equal(ProductMatchSource.NameAndPublisher, matcher.FindMatch(sameName, [msi with { UpgradeCode = null }])!.Source);
        Assert.Null(matcher.FindMatch(sameName with { Publisher = "Other" }, [msi with { UpgradeCode = null }]));
    }
    [Fact]
    public void MatchingUsesMsixIdentity()
    {
        var now = DateTimeOffset.UtcNow; var id = Guid.NewGuid(); var existing = new InstallerFile(1, 1, "a.msix", "a.msix", ".msix", 1, now, null, now, now, true, ProductId: id, MsixIdentityName: "Contoso.Sample");
        var match = new ProductMatchingService(new ProductNormalizer()).FindMatch(existing with { Id = 2, ProductId = null }, [existing]);
        Assert.Equal(ProductMatchSource.MsixIdentity, match!.Source); Assert.Equal(ProductMatchConfidence.High, match.Confidence);
    }
}
