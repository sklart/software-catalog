namespace SoftwareCatalog.Core.Domain;

public sealed record ScanRoot(long Id, string StoredPath, ScanRootPathKind PathKind, bool IncludeSubdirectories, bool Enabled, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc);
public enum ScanRootPathKind { Absolute, RelativeToApplication }
public enum ScanRootAvailability { Available, PathMissing }
