using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Infrastructure.Settings;

namespace SoftwareCatalog.Infrastructure.Paths;

public sealed class ArchiveLocationResolver(AppSettings settings, IAppPathService paths) : IArchiveLocationResolver
{
    public string ArchiveStoredPath => settings.ArchiveDestination;
    public ScanRootPathKind PathKind => settings.ArchiveDestinationKind == ArchiveDestinationKind.Absolute ? ScanRootPathKind.Absolute : ScanRootPathKind.RelativeToApplication;
    public string ArchiveRoot => settings.ArchiveDestinationKind == ArchiveDestinationKind.Absolute ? Path.GetFullPath(settings.ArchiveDestination) : Path.GetFullPath(Path.Combine(paths.ApplicationRoot, settings.ArchiveDestination));
    public string TrashRoot => Path.Combine(ArchiveRoot, "Trash");
}
