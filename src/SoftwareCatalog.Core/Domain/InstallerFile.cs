namespace SoftwareCatalog.Core.Domain;

public sealed record InstallerFile(string FullPath, string FileName, string Extension, long Size, DateTimeOffset LastWriteTimeUtc, string? Sha256, DateTimeOffset FirstSeenUtc, DateTimeOffset LastSeenUtc, bool Exists);
