using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Abstractions;

public interface IUpdateDownloadProvider
{
    string Id { get; }
    bool CanHandle(SoftwareProduct product, ProductUpdateSource source);
    Task<DownloadCandidateResolution> ResolveAsync(SoftwareProduct product, ProductUpdateSource source, UpdateCheckResult? update, CancellationToken cancellationToken);
}
