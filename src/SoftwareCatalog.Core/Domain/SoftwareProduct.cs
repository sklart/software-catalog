namespace SoftwareCatalog.Core.Domain;

public enum ProductMatchSource { MsiUpgradeCode, MsiProductCode, MsixIdentity, NameAndPublisher, FilenameFallback, Manual }
public enum ProductMatchConfidence { High, Medium, Low }
public enum UpdateStatus { Unknown, Checking, UpToDate, UpdateAvailable, LocalNewer, Ambiguous, NotFound, Error }
public enum VersionComparisonResult { Older, Equal, Newer, Unknown }

public sealed record SoftwareProduct(
    Guid Id,
    string CanonicalName,
    string? Publisher,
    string NormalizedName,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    string? LatestLocalVersion = null,
    string? LatestNormalizedVersion = null,
    UpdateStatus UpdateStatus = UpdateStatus.Unknown,
    string? LatestVersion = null,
    string? UpdateProvider = null,
    string? ExternalProductId = null,
    DateTimeOffset? LastCheckedUtc = null,
    string? UpdateError = null);

public sealed record ProductUpdateSource(Guid Id, Guid ProductId, string ProviderType, string ExternalId, bool Enabled = true, bool IsExplicit = false);
public sealed record UpdateCheckResult(UpdateStatus Status, string? LatestVersion = null, string? LatestNormalizedVersion = null, string? ReleaseName = null, DateTimeOffset? ReleaseDate = null, Uri? DownloadPageUrl = null, string? Source = null, string? ExternalProductId = null, string? Error = null, DateTimeOffset? CheckedUtc = null);
