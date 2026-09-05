namespace SoftwareCatalog.Core.Domain;

public enum InstallerKind { Unknown, Executable, Msi, Msix, MsixBundle, ZipArchive, SevenZipArchive }
public enum MetadataSource { None, PeVersionInfo, MsiDatabase, MsixManifest, FileNameFallback }
public enum MetadataStatus { NotProcessed, Success, Partial, Failed }
public enum InstallerStorageState { Active, Archived, Trashed }
public enum ArchiveOperationType { Archive, Restore, MoveToTrash, RestoreFromTrash, Purge }
public enum ArchiveOperationStatus { Planned, Running, Completed, Cancelled, Error, Conflict, AlreadyExists }

public sealed record InstallerFile(
    long Id,
    long ScanRootId,
    string RelativePath,
    string FileName,
    string Extension,
    long Size,
    DateTimeOffset LastWriteTimeUtc,
    string? Sha256,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    bool Exists,
    InstallerKind InstallerKind = InstallerKind.Unknown,
    string? ProductName = null,
    string? ProductVersion = null,
    string? Publisher = null,
    string? FileVersion = null,
    string? FileDescription = null,
    string? Architecture = null,
    MetadataSource MetadataSource = MetadataSource.None,
    MetadataStatus MetadataStatus = MetadataStatus.NotProcessed,
    string? MetadataError = null,
    string? NormalizedVersion = null,
    string? ProductCode = null,
    string? UpgradeCode = null,
    string? PackageList = null,
    Guid? ProductId = null,
    ProductMatchSource? ProductMatchSource = null,
    ProductMatchConfidence? ProductMatchConfidence = null,
    string? MsixIdentityName = null,
    InstallerStorageState StorageState = InstallerStorageState.Active,
    bool IsPinned = false,
    DateTimeOffset? StorageChangedUtc = null,
    long? OriginalScanRootId = null,
    string? OriginalRelativePath = null);

public sealed record ArchiveOperation(Guid Id, long InstallerId, Guid? ProductId, ArchiveOperationType OperationType,
    InstallerStorageState FromState, InstallerStorageState? ToState, string SourcePath, string? DestinationPath,
    string? Sha256, ArchiveOperationStatus Status, string? Error, DateTimeOffset StartedUtc, DateTimeOffset? CompletedUtc);

public sealed record InstallerStorageUpdate(long InstallerId, long ScanRootId, string RelativePath, InstallerStorageState StorageState,
    string? Sha256, long? OriginalScanRootId, string? OriginalRelativePath, DateTimeOffset ChangedUtc);
