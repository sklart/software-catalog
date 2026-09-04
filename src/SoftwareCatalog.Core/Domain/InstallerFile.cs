namespace SoftwareCatalog.Core.Domain;

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
    bool Exists);
