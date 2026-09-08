using Microsoft.Win32;
using System.Runtime.Versioning;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Scanner;

namespace SoftwareCatalog.Scanner.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsInstalledSoftwareSourceTests
{
    [Fact]
    public async Task ReadsEachRequiredSourceThroughTheTestSeam()
    {
        var source=new FakeSource(); var rows=await source.ReadAsync(CancellationToken.None);
        Assert.Contains((RegistryHive.LocalMachine,RegistryView.Registry64,InstalledSoftwareSource.Hklm64Uninstall),source.UninstallCalls); Assert.Contains((RegistryHive.LocalMachine,RegistryView.Registry32,InstalledSoftwareSource.Hklm32Uninstall),source.UninstallCalls); Assert.Contains((RegistryHive.CurrentUser,RegistryView.Default,InstalledSoftwareSource.HkcuUninstall),source.UninstallCalls); Assert.Equal(4,rows.Count); Assert.Contains(rows,row=>row.Source==InstalledSoftwareSource.Msi); Assert.Contains(rows,row=>row.Source==InstalledSoftwareSource.Msix);
    }
    [Fact]
    public async Task SourceFailureIsIsolatedButCancellationIsNot()
    {
        var source=new FakeSource { FailFirst=true }; var rows=await source.ReadAsync(CancellationToken.None); Assert.Equal(3,rows.Count);
        using var cts=new CancellationTokenSource(); cts.Cancel(); await Assert.ThrowsAsync<OperationCanceledException>(()=>source.ReadAsync(cts.Token));
    }
    private sealed class FakeSource : WindowsInstalledSoftwareSource
    {
        public bool FailFirst { get; set; } public List<(RegistryHive,RegistryView,InstalledSoftwareSource)> UninstallCalls { get; }=[];
        public FakeSource() : base(new ProductNormalizer()) { }
        protected override void ReadUninstall(RegistryHive hive,RegistryView view,InstalledSoftwareSource source,string? architecture,List<InstalledSoftware> result,CancellationToken token) { token.ThrowIfCancellationRequested(); UninstallCalls.Add((hive,view,source)); if(FailFirst && source==InstalledSoftwareSource.Hklm64Uninstall) throw new InvalidOperationException(); result.Add(Row(source==InstalledSoftwareSource.Hklm32Uninstall ? InstalledSoftwareSource.Msi : source)); }
        protected override void ReadMsix(List<InstalledSoftware> result,CancellationToken token) { token.ThrowIfCancellationRequested(); result.Add(Row(InstalledSoftwareSource.Msix)); }
        private static InstalledSoftware Row(InstalledSoftwareSource source) { var now=DateTimeOffset.UtcNow; return new(0,source.ToString(),"1.0",null,source.ToString(),"1.0",null,null,null,source,null,null,null,null,now,now,true); }
    }
}
