using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Tests;

public sealed class InstallerFileMetadataTests
{
    [Fact]
    public void PreservesCatalogMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        var file = new InstallerFile("D:\\Software\\tool.exe", "tool.exe", ".exe", 10, now, "ABC", now, now, true);
        Assert.Equal(".exe", file.Extension);
        Assert.True(file.Exists);
    }
}
