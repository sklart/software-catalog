namespace SoftwareCatalog.Core.Abstractions;

public interface IWritableDirectoryProbe { bool CanWrite(string directory, out string? error); }
