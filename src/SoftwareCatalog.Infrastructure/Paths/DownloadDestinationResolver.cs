using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Infrastructure.Settings;

namespace SoftwareCatalog.Infrastructure.Paths;

public static class DownloadDestinationResolver
{
    public static string Resolve(AppSettings settings, IAppPathService paths) => settings.DownloadDestinationKind == DownloadDestinationKind.Absolute ? Path.GetFullPath(settings.DownloadDestination) : Path.GetFullPath(Path.Combine(paths.ApplicationRoot, settings.DownloadDestination));
}
