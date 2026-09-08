using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.UI;

namespace SoftwareCatalog.UI.Tests;

public sealed class Stage7InventoryUiTests
{
    [Fact]
    public void ManualBindChangeAndClearProduceRepositoryBindings()
    {
        var first=Guid.NewGuid(); var second=Guid.NewGuid(); var bind=InstalledSoftwareWorkflow.Bind(first); var change=InstalledSoftwareWorkflow.Bind(second); var clear=InstalledSoftwareWorkflow.Clear();
        Assert.Equal(first,bind.ProductId); Assert.Equal(InstalledSoftwareMatchSource.Manual,bind.Source); Assert.Equal(second,change.ProductId); Assert.Equal(InstalledSoftwareMatchConfidence.Exact,change.Confidence); Assert.Null(clear.ProductId); Assert.Equal(InstalledSoftwareMatchSource.ManualReview,clear.Source);
    }
    [Fact]
    public void InventoryRowsExposeLatestVersionUpdateStatusAndProductSummary()
    {
        var now = DateTimeOffset.UtcNow; var product = new SoftwareProduct(Guid.NewGuid(), "Tool", "Vendor", "tool", now, now, LatestVersion: "2.0", LatestNormalizedVersion: "2.0");
        var installed = new InstalledSoftware(1, "Tool", "1.0", "Vendor", "tool", "1.0", null, null, "x64", InstalledSoftwareSource.Hklm64Uninstall, null, product.Id, InstalledSoftwareMatchSource.Manual, InstalledSoftwareMatchConfidence.Exact, now, now, true);
        var presentation = new InstalledSoftwarePresentationService(new VersionComparer()); var row = Assert.Single(presentation.CreateRows([installed], [product])); var summary = presentation.ApplyProductSummaries([product], [row])[product.Id];
        Assert.Equal("2.0", row.LatestVersion); Assert.Equal(InstalledUpdateStatus.UpdateAvailable, row.UpdateStatus); Assert.True(summary.IsInstalled); Assert.Equal(1, summary.InstalledCopiesCount); Assert.Equal("x64", summary.InstalledArchitectures);
    }

    [Fact]
    public void WorkflowFiltersSelectActualInventoryRows()
    {
        var now=DateTimeOffset.UtcNow; var product=Guid.NewGuid(); var update=Row(product, InstalledUpdateStatus.UpdateAvailable, InstalledSoftwareMatchConfidence.Exact, true, now); var unmatched=Row(null, InstalledUpdateStatus.UpToDate, InstalledSoftwareMatchConfidence.None, true, now); var problem=Row(null, InstalledUpdateStatus.Unknown, InstalledSoftwareMatchConfidence.Ambiguous, true, now);
        Assert.True(InstalledSoftwareWorkflow.Includes(update, InstalledSoftwareFilter.UpdatesAvailable)); Assert.True(InstalledSoftwareWorkflow.Includes(update, InstalledSoftwareFilter.Matched)); Assert.True(InstalledSoftwareWorkflow.Includes(unmatched, InstalledSoftwareFilter.Unmatched)); Assert.True(InstalledSoftwareWorkflow.Includes(problem, InstalledSoftwareFilter.Problems)); Assert.False(InstalledSoftwareWorkflow.Includes(update, InstalledSoftwareFilter.Problems));
    }

    [Fact]
    public void ProductColumnsAreDeclaredForMainGrid() => Assert.Equal(["Installed", "Installed versions", "Installed copies", "Architectures", "Installed update status"], InstalledSoftwareWorkflow.ProductColumnHeaders);

    private static InstalledSoftwareRow Row(Guid? productId, InstalledUpdateStatus status, InstalledSoftwareMatchConfidence confidence, bool exists, DateTimeOffset now) => new(new InstalledSoftware(1, "Tool", "1.0", "Vendor", "tool", "1.0", null, null, "x64", InstalledSoftwareSource.Hklm64Uninstall, null, productId, InstalledSoftwareMatchSource.ManualReview, confidence, now, now, exists), productId is null ? null : "Tool", "2.0", status);
}
