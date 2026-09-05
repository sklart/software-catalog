using System.IO.Compression;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Scanner;

namespace SoftwareCatalog.Scanner.Tests;

public sealed class InstallerMetadataServiceTests
{
    [Theory]
    [InlineData("setup.exe", InstallerKind.Executable)]
    [InlineData("setup.msi", InstallerKind.Msi)]
    [InlineData("app.msix", InstallerKind.Msix)]
    [InlineData("bundle.msixbundle", InstallerKind.MsixBundle)]
    [InlineData("archive.zip", InstallerKind.ZipArchive)]
    [InlineData("archive.7z", InstallerKind.SevenZipArchive)]
    public void IdentifiesSupportedInstallerKinds(string path, InstallerKind expected) => Assert.Equal(expected, InstallerMetadataService.Identify(path));

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("Version 1.2.3.0", "1.2.3.0")]
    [InlineData("release candidate", null)]
    public void NormalizesConservativeVersions(string raw, string? expected) => Assert.Equal(expected, VersionNormalizer.Normalize(raw));

    [Fact]
    public void ParsesKnownFilenameWithoutGuessingUnknownVersion()
    {
        var known = FilenameParser.Parse("7zip-26.00-x64");
        Assert.Equal("7zip", known.ProductName); Assert.Equal("26.00", known.ProductVersion); Assert.Equal("x64", known.Architecture);
        Assert.Null(FilenameParser.Parse("just-a-tool").ProductVersion);
    }

    [Fact]
    public async Task ExtractsMsixManifestWithoutInstallingPackage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.msix");
        try
        {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("AppxManifest.xml").Open()))
                await writer.WriteAsync("<Package xmlns='http://schemas.microsoft.com/appx/manifest/foundation/windows10'><Identity Name='Sample.App' Version='1.2.3.0' Publisher='CN=Sample' ProcessorArchitecture='x64'/><Properties><DisplayName>Sample App</DisplayName><PublisherDisplayName>Sample Corp</PublisherDisplayName></Properties></Package>");
            var metadata = new MsixMetadataExtractor().Extract(path, InstallerKind.Msix);
            Assert.Equal(MetadataStatus.Success, metadata.Status); Assert.Equal("Sample App", metadata.ProductName); Assert.Equal("1.2.3.0", metadata.ProductVersion); Assert.Equal("Sample Corp", metadata.Publisher); Assert.Equal("x64", metadata.Architecture);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task MalformedMsixReturnsFailureInsteadOfThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.msix");
        try { await File.WriteAllTextAsync(path, "not-a-zip"); var result = new MsixMetadataExtractor().Extract(path, InstallerKind.Msix); Assert.Equal(MetadataStatus.Failed, result.Status); Assert.NotNull(result.Error); }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task MalformedExecutableReturnsFailureInsteadOfThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");
        try { await File.WriteAllTextAsync(path, "not-a-pe"); var result = new PeMetadataExtractor().Extract(path, InstallerKind.Executable); Assert.Equal(MetadataStatus.Failed, result.Status); }
        finally { File.Delete(path); }
    }
}
