namespace SoftwareCatalog.Core.Domain;

public enum DownloadCandidateStatus { Available, NotFound, Ambiguous, Unsupported, Error }
public enum DownloadStatus { Queued, Resolving, Downloading, Verifying, Validating, Completed, Cancelled, Error, AlreadyExists, ValidationFailed, ManualConfirmationRequired }
public enum DownloadDestinationKind { Absolute, RelativeToApplication }

public sealed record DownloadCandidate(string Provider, string ExternalProductId, string? Version, string? NormalizedVersion, string? FileName, Uri? Uri, string? ContentType = null, long? Size = null, string? Architecture = null, string? InstallerKind = null, string? Sha256 = null, string? Sha256Source = null, string? ReleaseName = null, DateTimeOffset? ReleaseDate = null);
public sealed record DownloadCandidateResolution(DownloadCandidateStatus Status, IReadOnlyList<DownloadCandidate> Candidates, string? Error = null);
public sealed record DownloadProgress(long BytesReceived, long? TotalBytes, double? Percent, double? SpeedBytesPerSecond, DownloadStatus Status, string? FileName = null);
public sealed record DownloadHistory(Guid Id, Guid ProductId, string ProviderType, string ExternalProductId, string? Version, string? FileName, string? SourceUrl, string? ExpectedSha256, string? ActualSha256, DownloadStatus Status, string? Error, DateTimeOffset StartedUtc, DateTimeOffset? CompletedUtc, string? FinalRelativePath);
public sealed record DownloadResult(DownloadStatus Status, string? FinalPath, string? Sha256, string? Error = null);
