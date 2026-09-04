namespace SoftwareCatalog.Core.Domain;

public sealed record SoftwareProduct(Guid Id, string Name, string? Publisher = null);
