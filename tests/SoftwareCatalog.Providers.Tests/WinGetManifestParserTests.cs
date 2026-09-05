using SoftwareCatalog.Providers;

namespace SoftwareCatalog.Providers.Tests;

public sealed class WinGetManifestParserTests
{
    [Fact]
    public void ParsesMultipleInstallerEntriesAndOptionalFields()
    {
        const string manifest = """
            PackageIdentifier: Vendor.Tool
            Installers:
              - Architecture: x64
                InstallerType: msi
                InstallerUrl: https://example.test/tool-x64.msi
                InstallerSha256: AABB
                Scope: machine
                InstallerLocale: en-US
              - Architecture: arm64
                InstallerType: exe
                InstallerUrl: https://example.test/tool-arm64.exe
                InstallerSha256: CCDD
            """;
        var installers = WinGetManifestParser.Parse(manifest);
        Assert.Equal(2, installers.Count); Assert.Equal("https://example.test/tool-x64.msi", installers[0].InstallerUrl); Assert.Equal("AABB", installers[0].InstallerSha256); Assert.Equal("x64", installers[0].Architecture); Assert.Equal("msi", installers[0].InstallerType); Assert.Equal("machine", installers[0].Scope); Assert.Equal("en-US", installers[0].Locale); Assert.Equal("arm64", installers[1].Architecture);
    }
    [Fact]
    public void IgnoresEntriesWithoutInstallerUrl() => Assert.Empty(WinGetManifestParser.Parse("Installers:\n  - Architecture: x64\n    InstallerType: msi"));
    [Fact]
    public void ParsesOrdinaryWinGetShowInstallerBlockWithSpacesAndExtraLines()
    {
        const string output = """
            Found Tool [Vendor.Tool]
            Version: 2.0
            Installer:
              Installer Type: MSI
              Installer Url: https://example.test/tool-x64.msi
              Installer SHA256: AABBCC
              Architecture: x64
              Scope: machine
              Installer Locale: en-US
            Downloading is not installation.
            """;
        var installer = Assert.Single(WinGetManifestParser.Parse(output)); Assert.Equal("https://example.test/tool-x64.msi", installer.InstallerUrl); Assert.Equal("AABBCC", installer.InstallerSha256); Assert.Equal("MSI", installer.InstallerType); Assert.Equal("x64", installer.Architecture); Assert.Equal("machine", installer.Scope); Assert.Equal("en-US", installer.Locale);
    }
}
