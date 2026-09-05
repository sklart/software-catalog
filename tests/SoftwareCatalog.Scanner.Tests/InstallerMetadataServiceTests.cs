using System.IO.Compression;
using System.Runtime.InteropServices;
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
    public void NormalizesNullToNull() => Assert.Null(VersionNormalizer.Normalize(null));

    [Theory]
    [InlineData("1.2", "1.2")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("V1.2.3", "1.2.3")]
    [InlineData("version 1.2.3.4", "1.2.3.4")]
    [InlineData("  1.2.3  ", "1.2.3")]
    [InlineData("1", null)]
    [InlineData("1.2-beta", null)]
    [InlineData("release-1.2", null)]
    [InlineData("latest", null)]
    [InlineData("2026 build", null)]
    [InlineData("", null)]
    public void NormalizerHandlesSpecifiedEdgeCases(string raw, string? expected) => Assert.Equal(expected, VersionNormalizer.Normalize(raw));

    [Fact]
    public void ParsesKnownFilenameWithoutGuessingUnknownVersion()
    {
        var known = FilenameParser.Parse("7zip-26.00-x64");
        Assert.Equal("7zip", known.ProductName); Assert.Equal("26.00", known.ProductVersion); Assert.Equal("x64", known.Architecture);
        Assert.Null(FilenameParser.Parse("just-a-tool").ProductVersion);
    }

    [Theory]
    [InlineData("foobar_1.4.2_setup", "foobar", "1.4.2", null)]
    [InlineData("ProgramName-v3.8.1-win64", "ProgramName", "3.8.1", "x64")]
    [InlineData("Tool.Setup.5.2.0", "Tool", "5.2.0", null)]
    public void ParsesAdditionalKnownFilenamePatterns(string fileName, string product, string version, string? architecture)
    {
        var result = FilenameParser.Parse(fileName);
        Assert.Equal(product, result.ProductName); Assert.Equal(version, result.ProductVersion); Assert.Equal(architecture, result.Architecture);
    }

    [Theory]
    [InlineData("tool-final")]
    [InlineData("program-2026")]
    [InlineData("random-build")]
    [InlineData("setup-latest")]
    public void DoesNotInventAmbiguousFilenameVersion(string fileName) => Assert.Null(FilenameParser.Parse(fileName).ProductVersion);

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
        try { await File.WriteAllTextAsync(path, "not-a-pe"); var result = new PeMetadataExtractor().Extract(path, InstallerKind.Executable); Assert.Equal(MetadataStatus.Failed, result.Status); Assert.False(string.IsNullOrWhiteSpace(result.Error)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ReadsVersionInfoFromRealManagedPeFixture()
    {
        var path = typeof(InstallerMetadataService).Assembly.Location;
        var result = new PeMetadataExtractor().Extract(path, InstallerKind.Executable);
        Assert.Equal(MetadataStatus.Success, result.Status); Assert.False(string.IsNullOrWhiteSpace(result.ProductName)); Assert.False(string.IsNullOrWhiteSpace(result.ProductVersion)); Assert.False(string.IsNullOrWhiteSpace(result.Publisher)); Assert.False(string.IsNullOrWhiteSpace(result.FileVersion)); Assert.False(string.IsNullOrWhiteSpace(result.FileDescription));
    }

    [Theory]
    [InlineData((ushort)0x014c, "x86")]
    [InlineData((ushort)0x8664, "x64")]
    public async Task ReadsArchitectureFromMinimalPeHeaders(ushort machine, string expected)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");
        try
        {
            var bytes = new byte[0x100]; bytes[0] = 0x4D; bytes[1] = 0x5A; BitConverter.GetBytes(0x80).CopyTo(bytes, 0x3C); bytes[0x80] = 0x50; bytes[0x81] = 0x45; BitConverter.GetBytes(machine).CopyTo(bytes, 0x84); await File.WriteAllBytesAsync(path, bytes);
            Assert.Equal(expected, PeMetadataExtractor.ReadArchitecture(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExtractsBundleArchitecturesFromAllPackageEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.msixbundle");
        try
        {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("AppxMetadata/AppxBundleManifest.xml").Open()))
                await writer.WriteAsync("<Bundle xmlns='http://schemas.microsoft.com/appx/2013/bundle'><Identity Name='Sample.Bundle' Version='2.0.0.0' Publisher='CN=Sample'/><Packages><Package FileName='x86.msix' Architecture='x86'/><Package FileName='x64.msix' Architecture='x64'/><Package FileName='arm64.msix' Architecture='ARM64'/></Packages></Bundle>");
            var result = new MsixMetadataExtractor().Extract(path, InstallerKind.MsixBundle);
            Assert.Equal(MetadataStatus.Success, result.Status); Assert.Equal("Sample.Bundle", result.ProductName); Assert.Equal("2.0.0.0", result.ProductVersion); Assert.Equal("CN=Sample", result.Publisher); Assert.Equal("x86,x64,ARM64", result.Architecture); Assert.Equal("arm64.msix,x64.msix,x86.msix", result.PackageList);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task MalformedBundleReturnsFailureInsteadOfThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.msixbundle");
        try { await File.WriteAllTextAsync(path, "broken"); var result = new MsixMetadataExtractor().Extract(path, InstallerKind.MsixBundle); Assert.Equal(MetadataStatus.Failed, result.Status); Assert.NotNull(result.Error); }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ReadsPropertiesFromControlledMsiFixtureWithoutInstalling()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.msi");
        try
        {
            MsiFixture.Create(path);
            var result = new MsiMetadataExtractor().Extract(path, InstallerKind.Msi);
            Assert.Equal(MetadataStatus.Success, result.Status); Assert.Equal("Sample Product", result.ProductName); Assert.Equal("1.2.3", result.ProductVersion); Assert.Equal("Sample Publisher", result.Publisher); Assert.Equal("{11111111-1111-1111-1111-111111111111}", result.ProductCode); Assert.Equal("{22222222-2222-2222-2222-222222222222}", result.UpgradeCode);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task MalformedMsiReturnsFailureInsteadOfThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.msi");
        try { await File.WriteAllTextAsync(path, "broken"); var result = new MsiMetadataExtractor().Extract(path, InstallerKind.Msi); Assert.Equal(MetadataStatus.Failed, result.Status); Assert.NotNull(result.Error); }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("broken.exe", InstallerKind.Executable)]
    [InlineData("broken.msi", InstallerKind.Msi)]
    [InlineData("broken.msix", InstallerKind.Msix)]
    public async Task KeepsKindForMalformedFiles(string name, InstallerKind expected)
    {
        var path = Path.Combine(Path.GetTempPath(), name + Guid.NewGuid().ToString("N"));
        path = Path.ChangeExtension(path, Path.GetExtension(name));
        try { await File.WriteAllTextAsync(path, "broken"); Assert.Equal(expected, new InstallerMetadataService().Extract(path).Kind); }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ZipAndSevenZipRemainShallowPartialMetadata()
    {
        foreach (var extension in new[] { ".zip", ".7z" })
        {
            var path = Path.Combine(Path.GetTempPath(), $"archive{Guid.NewGuid():N}{extension}");
            try { await File.WriteAllTextAsync(path, "not inspected"); var result = new InstallerMetadataService().Extract(path); Assert.Equal(extension == ".zip" ? InstallerKind.ZipArchive : InstallerKind.SevenZipArchive, result.Kind); Assert.Equal(MetadataStatus.Partial, result.Status); }
            finally { File.Delete(path); }
        }
    }

    private static class MsiFixture
    {
        public static void Create(string path)
        {
            Assert.Equal(0U, MsiOpenDatabase(path, new IntPtr(3), out var database));
            try
            {
                Execute(database, "CREATE TABLE `Property` (`Property` CHAR(72) NOT NULL, `Value` CHAR(0) NOT NULL PRIMARY KEY `Property`)");
                foreach (var (key, value) in new[] { ("ProductName", "Sample Product"), ("ProductVersion", "1.2.3"), ("Manufacturer", "Sample Publisher"), ("ProductCode", "{11111111-1111-1111-1111-111111111111}"), ("UpgradeCode", "{22222222-2222-2222-2222-222222222222}") }) Execute(database, $"INSERT INTO `Property` (`Property`,`Value`) VALUES ('{key}','{value}')");
                Assert.Equal(0U, MsiDatabaseCommit(database));
            }
            finally { MsiCloseHandle(database); }
        }
        private static void Execute(IntPtr database, string sql) { Assert.Equal(0U, MsiDatabaseOpenView(database, sql, out var view)); try { Assert.Equal(0U, MsiViewExecute(view, IntPtr.Zero)); } finally { MsiCloseHandle(view); } }
        [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern uint MsiOpenDatabase(string path, IntPtr persist, out IntPtr database);
        [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern uint MsiDatabaseOpenView(IntPtr database, string query, out IntPtr view);
        [DllImport("msi.dll")] private static extern uint MsiViewExecute(IntPtr view, IntPtr record);
        [DllImport("msi.dll")] private static extern uint MsiDatabaseCommit(IntPtr database);
        [DllImport("msi.dll")] private static extern uint MsiCloseHandle(IntPtr handle);
    }
}
