namespace SoftwareCatalog.Core.Domain;

public enum ProductMatchSource { MsiUpgradeCode, MsiProductCode, MsixIdentity, NameAndPublisher, FilenameFallback, Manual }
public enum ProductMatchConfidence { High, Medium, Low }
public enum UpdateStatus { Unknown, Checking, UpToDate, UpdateAvailable, LocalNewer, Ambiguous, NotFound, Error }
public enum VersionComparisonResult { Older, Equal, Newer, Unknown }
public enum MappingConfidence { Exact, High, Medium, Low, Ambiguous }
public enum MappingSource { Manual, Imported, ExactMatch, NormalizedName, PublisherName, ProviderSearch }
public enum ProviderErrorKind { Timeout, RateLimited, NotFound, AuthenticationRequired, NetworkError, InvalidResponse, Ambiguous }
public enum InstalledSoftwareSource { Hklm64Uninstall, Hklm32Uninstall, HkcuUninstall, Msi, Msix }
public enum InstalledSoftwareMatchSource { Explicit, ExternalIdentity, NameAndPublisher, Alias, NormalizedName, ManualReview, Manual }
public enum InstalledSoftwareMatchConfidence { None, Low, Medium, High, Exact, Ambiguous }
public enum InstalledUpdateStatus { Unknown, UpdateAvailable, UpToDate, InstalledNewer }

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

public sealed record ProductUpdateSource(Guid Id, Guid ProductId, string ProviderType, string ExternalId, bool Enabled = true, bool IsExplicit = false, MappingSource Source = MappingSource.Manual, MappingConfidence Confidence = MappingConfidence.Exact, DateTimeOffset? CreatedUtc = null, DateTimeOffset? UpdatedUtc = null);
public sealed record ProductAlias(Guid ProductId, string Alias, string NormalizedAlias, MappingSource Source = MappingSource.ProviderSearch);
public sealed record UpdateCandidate(string ProviderType, string ExternalId, string DisplayName, string? PublisherOrOwner, string? LatestVersion, MappingConfidence Confidence, string Reason);
public sealed record UpdateCheckResult(UpdateStatus Status, string? LatestVersion = null, string? LatestNormalizedVersion = null, string? ReleaseName = null, DateTimeOffset? ReleaseDate = null, Uri? DownloadPageUrl = null, string? Source = null, string? ExternalProductId = null, string? Error = null, DateTimeOffset? CheckedUtc = null, ProviderErrorKind? ErrorKind = null);
public sealed record InstalledSoftware(long Id, string DisplayName, string? DisplayVersion, string? Publisher, string NormalizedName, string? NormalizedVersion, string? InstallLocation, DateTimeOffset? InstallDate, string? Architecture, InstalledSoftwareSource Source, string? ExternalId, Guid? ProductId, InstalledSoftwareMatchSource? MatchSource, InstalledSoftwareMatchConfidence? MatchConfidence, DateTimeOffset FirstSeenUtc, DateTimeOffset LastSeenUtc, bool Exists);
public sealed record InstalledSoftwareStatus(InstalledSoftware Installed, string? LatestVersion, InstalledUpdateStatus UpdateStatus);
