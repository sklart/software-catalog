namespace SoftwareCatalog.Core.Domain;

public enum InstallerKind { Unknown, Executable, Msi, Msix, MsixBundle, ZipArchive, SevenZipArchive }
public enum MetadataSource { None, PeVersionInfo, MsiDatabase, MsixManifest, FileNameFallback }
public enum MetadataStatus { NotProcessed, Success, Partial, Failed }

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
    string? PackageList = null);
