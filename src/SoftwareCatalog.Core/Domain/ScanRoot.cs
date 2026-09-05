namespace SoftwareCatalog.Core.Domain;

public sealed record ScanRoot(long Id, string StoredPath, ScanRootPathKind PathKind, bool IncludeSubdirectories, bool Enabled, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc, ScanRootRole Role = ScanRootRole.User);
public enum ScanRootPathKind { Absolute, RelativeToApplication }
public enum ScanRootRole { User, Archive, Trash }
public enum ScanRootAvailability { Available, PathMissing }
