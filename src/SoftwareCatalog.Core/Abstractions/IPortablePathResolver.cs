using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Abstractions;

public interface IPortablePathResolver
{
    string Resolve(ScanRoot root);
    string ToStoredPath(string path, ScanRootPathKind kind);
    string GetRelativePath(ScanRoot root, string fullPath);
    ScanRootAvailability GetAvailability(ScanRoot root);
}
