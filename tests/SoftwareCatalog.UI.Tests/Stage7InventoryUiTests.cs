using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI.Tests;

public sealed class Stage7InventoryUiTests
{
    [Fact]
    public void InventoryRowsExposeLatestVersionUpdateStatusAndProductSummary()
    {
        var now = DateTimeOffset.UtcNow; var product = new SoftwareProduct(Guid.NewGuid(), "Tool", "Vendor", "tool", now, now, LatestVersion: "2.0", LatestNormalizedVersion: "2.0");
        var installed = new InstalledSoftware(1, "Tool", "1.0", "Vendor", "tool", "1.0", null, null, "x64", InstalledSoftwareSource.Hklm64Uninstall, null, product.Id, InstalledSoftwareMatchSource.Manual, InstalledSoftwareMatchConfidence.Exact, now, now, true);
        var presentation = new InstalledSoftwarePresentationService(new VersionComparer()); var row = Assert.Single(presentation.CreateRows([installed], [product])); var summary = presentation.ApplyProductSummaries([product], [row])[product.Id];
        Assert.Equal("2.0", row.LatestVersion); Assert.Equal(InstalledUpdateStatus.UpdateAvailable, row.UpdateStatus); Assert.True(summary.IsInstalled); Assert.Equal(1, summary.InstalledCopiesCount); Assert.Equal("x64", summary.InstalledArchitectures);
    }

    [Theory]
    [InlineData(InstalledUpdateStatus.UpdateAvailable, true)]
    [InlineData(InstalledUpdateStatus.UpToDate, false)]
    public void UpdatesAvailableFilterSelectsOnlyUpdateRows(InstalledUpdateStatus status, bool expected) => Assert.Equal(expected, status == InstalledUpdateStatus.UpdateAvailable);

    [Theory]
    [InlineData(true, true)] [InlineData(false, false)]
    public void MatchedAndUnmatchedFiltersUseProductBinding(bool matched, bool expected) => Assert.Equal(expected, matched);

    [Theory]
    [InlineData(InstalledSoftwareMatchConfidence.Ambiguous, true)] [InlineData(InstalledSoftwareMatchConfidence.Low, true)] [InlineData(InstalledSoftwareMatchConfidence.Exact, false)]
    public void ProblemsFilterFlagsAmbiguousAndLowMatches(InstalledSoftwareMatchConfidence confidence, bool expected) => Assert.Equal(expected, confidence is InstalledSoftwareMatchConfidence.Ambiguous or InstalledSoftwareMatchConfidence.Low);
}
