using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Infrastructure.Paths;

public sealed class PortablePathResolver(IAppPathService paths) : IPortablePathResolver
{
    public string Resolve(ScanRoot root) => root.PathKind == ScanRootPathKind.Absolute ? Path.GetFullPath(root.StoredPath) : Path.GetFullPath(Path.Combine(paths.ApplicationRoot, root.StoredPath));
    public string ToStoredPath(string path, ScanRootPathKind kind) => kind == ScanRootPathKind.Absolute ? Path.GetFullPath(path) : Path.GetRelativePath(paths.ApplicationRoot, Path.GetFullPath(path));
    public string GetRelativePath(ScanRoot root, string fullPath) => Path.GetRelativePath(Resolve(root), fullPath);
    public ScanRootAvailability GetAvailability(ScanRoot root) => Directory.Exists(Resolve(root)) ? ScanRootAvailability.Available : ScanRootAvailability.PathMissing;
}
