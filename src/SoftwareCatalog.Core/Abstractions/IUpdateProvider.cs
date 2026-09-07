using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Abstractions;

public interface IUpdateProvider
{
    string Id { get; }
    bool CanHandle(SoftwareProduct product, ProductUpdateSource? source);
    Task<UpdateCheckResult> CheckLatestAsync(SoftwareProduct product, ProductUpdateSource? source, CancellationToken cancellationToken);
}
public interface IUpdateCandidateProvider
{
    Task<IReadOnlyList<UpdateCandidate>> SearchCandidatesAsync(SoftwareProduct product, IReadOnlyList<ProductAlias> aliases, CancellationToken cancellationToken);
}
