using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI;

public enum InstalledSoftwareFilter { All, UpdatesAvailable, Matched, Unmatched, Problems }

public sealed record ProductColumnDescriptor(string Header, string BindingProperty, bool IsCheckBox = false);

public static class InstalledSoftwareWorkflow
{
    public static readonly (string Label, InstalledSoftwareFilter Filter)[] Filters = [("All", InstalledSoftwareFilter.All), ("Updates available", InstalledSoftwareFilter.UpdatesAvailable), ("Matched", InstalledSoftwareFilter.Matched), ("Unmatched", InstalledSoftwareFilter.Unmatched), ("Problems", InstalledSoftwareFilter.Problems)];
    public static readonly ProductColumnDescriptor[] ProductColumns = [new("Installed", "IsInstalled", true), new("Installed versions", "InstalledVersions"), new("Installed copies", "InstalledCopiesCount"), new("Architectures", "InstalledArchitectures"), new("Installed update status", "InstalledUpdateStatus")];
    public static bool Includes(InstalledSoftwareRow row, InstalledSoftwareFilter filter) => filter switch
    {
        InstalledSoftwareFilter.All => true,
        InstalledSoftwareFilter.UpdatesAvailable => row.UpdateStatus == InstalledUpdateStatus.UpdateAvailable,
        InstalledSoftwareFilter.Matched => row.ProductId is not null,
        InstalledSoftwareFilter.Unmatched => row.ProductId is null,
        InstalledSoftwareFilter.Problems => !row.Exists || row.MatchConfidence is InstalledSoftwareMatchConfidence.Ambiguous or InstalledSoftwareMatchConfidence.Low,
        _ => false
    };

    public static (Guid? ProductId, InstalledSoftwareMatchSource Source, InstalledSoftwareMatchConfidence Confidence) Bind(Guid productId) => (productId, InstalledSoftwareMatchSource.Manual, InstalledSoftwareMatchConfidence.Exact);
    public static (Guid? ProductId, InstalledSoftwareMatchSource Source, InstalledSoftwareMatchConfidence Confidence) Clear() => (null, InstalledSoftwareMatchSource.ManualReview, InstalledSoftwareMatchConfidence.None);
}
