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
    [Fact]
    public void AvailabilityUsesResolverForAbsoluteAndRelativeRoots()
    {
        var basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); var app = Path.Combine(basePath, "App"); var absolute = Path.Combine(basePath, "Absolute"); var relative = Path.Combine(basePath, "Relative"); Directory.CreateDirectory(app); Directory.CreateDirectory(absolute); Directory.CreateDirectory(relative);
        try { var resolver = new PortablePathResolver(new PortableAppPathService(app)); Assert.Equal(ScanRootAvailability.Available, resolver.GetAvailability(new ScanRoot(1, absolute, ScanRootPathKind.Absolute, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow))); Assert.Equal(ScanRootAvailability.PathMissing, resolver.GetAvailability(new ScanRoot(2, Path.Combine(basePath, "Missing"), ScanRootPathKind.Absolute, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow))); Assert.Equal(ScanRootAvailability.Available, resolver.GetAvailability(new ScanRoot(3, "..\\Relative", ScanRootPathKind.RelativeToApplication, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow))); Assert.Equal(ScanRootAvailability.PathMissing, resolver.GetAvailability(new ScanRoot(4, "..\\Missing", ScanRootPathKind.RelativeToApplication, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow))); } finally { Directory.Delete(basePath, true); }
    }
}
