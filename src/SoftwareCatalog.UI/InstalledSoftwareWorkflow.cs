using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI;

public enum InstalledSoftwareFilter { All, UpdatesAvailable, Matched, Unmatched, Problems }

public static class InstalledSoftwareWorkflow
{
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
