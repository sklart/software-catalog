using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Infrastructure.Paths;

namespace SoftwareCatalog.Infrastructure.Tests;
public sealed class PortablePathResolverTests
{
    [Fact]
    public void RelativeRootMovesWithApplication()
    {
        var rootA = Path.Combine(Path.GetTempPath(), "DriveA", "SoftwareCatalog"); var rootB = Path.Combine(Path.GetTempPath(), "DriveB", "SoftwareCatalog");
        var scanRoot = new ScanRoot(1, "..\\Software", ScanRootPathKind.RelativeToApplication, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "DriveA", "Software"), new PortablePathResolver(new PortableAppPathService(rootA)).Resolve(scanRoot));
        Assert.Equal(Path.Combine(Path.GetTempPath(), "DriveB", "Software"), new PortablePathResolver(new PortableAppPathService(rootB)).Resolve(scanRoot));
    }
}
