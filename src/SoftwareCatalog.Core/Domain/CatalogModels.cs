namespace SoftwareCatalog.Core.Domain;

public sealed record SoftwareVersion(Guid Id, Guid ProductId, string Version, string Channel);
public sealed record UpdateSource(Guid Id, Guid ProductId, string ProviderId, string ExternalId);
public sealed record UpdateCheck(Guid Id, Guid ProductId, string Status, DateTimeOffset CheckedAtUtc);
public sealed record SoftwareAlias(Guid Id, Guid ProductId, string Alias, bool IsConfirmed);
public sealed record MatchCandidate(Guid Id, Guid InstallerFileId, Guid ProductId, int Confidence);
