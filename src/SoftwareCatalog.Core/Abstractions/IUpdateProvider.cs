using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Abstractions;

public interface IUpdateProvider { string Id { get; } Task<UpdateResult> CheckAsync(SoftwareProduct product, CancellationToken cancellationToken); }
public sealed record UpdateResult(string? LatestVersion, string Status, Uri? ReleasePage = null);
