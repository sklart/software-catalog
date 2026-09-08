using Microsoft.Win32;
using System.Runtime.Versioning;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Scanner;

/// <summary>Reads Windows inventory only; it never invokes installers, package managers, or shell commands.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsInstalledSoftwareSource(ProductNormalizer normalizer) : IInstalledSoftwareSource
{
    private const string Uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    public Task<IReadOnlyList<InstalledSoftware>> ReadAsync(CancellationToken token)
    {
        var result = new List<InstalledSoftware>();
        ReadUninstall(RegistryHive.LocalMachine, RegistryView.Registry64, InstalledSoftwareSource.Hklm64Uninstall, "x64", result, token);
        ReadUninstall(RegistryHive.LocalMachine, RegistryView.Registry32, InstalledSoftwareSource.Hklm32Uninstall, "x86", result, token);
        ReadUninstall(RegistryHive.CurrentUser, RegistryView.Default, InstalledSoftwareSource.HkcuUninstall, Environment.Is64BitOperatingSystem ? "x64" : "x86", result, token);
        ReadMsix(result, token);
        return Task.FromResult<IReadOnlyList<InstalledSoftware>>(result);
    }
    private void ReadUninstall(RegistryHive hive, RegistryView view, InstalledSoftwareSource source, string defaultArchitecture, List<InstalledSoftware> result, CancellationToken token)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view); using var uninstall = baseKey.OpenSubKey(Uninstall, false); if (uninstall is null) return;
        foreach (var name in uninstall.GetSubKeyNames()) { token.ThrowIfCancellationRequested(); using var key=uninstall.OpenSubKey(name, false); if (key is null) continue; var display=key.GetValue("DisplayName") as string; if(string.IsNullOrWhiteSpace(display)) continue; var version=key.GetValue("DisplayVersion") as string; var publisher=key.GetValue("Publisher") as string; var location=key.GetValue("InstallLocation") as string; var date=ParseDate(key.GetValue("InstallDate") as string); var architecture=(key.GetValue("Architecture") as string) ?? defaultArchitecture; var itemSource=Convert.ToInt32(key.GetValue("WindowsInstaller",0)) == 1 ? InstalledSoftwareSource.Msi : source; result.Add(New(display,version,publisher,location,date,architecture,itemSource,name)); }
    }
    private void ReadMsix(List<InstalledSoftware> result, CancellationToken token)
    {
        // Package repository registry is readable without PowerShell and contains only registered packages.
        using var baseKey=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64); using var packages=baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel\StateRepository\Cache\Package\Data",false); if(packages is null)return;
        foreach(var family in packages.GetSubKeyNames()) { token.ThrowIfCancellationRequested(); using var key=packages.OpenSubKey(family,false); var full=key?.GetValue("PackageFullName") as string; if(string.IsNullOrWhiteSpace(full))continue; var parts=full.Split('_'); if(parts.Length<2)continue; result.Add(New(parts[0],parts[1],null,null,null,Environment.Is64BitOperatingSystem?"x64":"x86",InstalledSoftwareSource.Msix,family)); }
    }
    private InstalledSoftware New(string name,string? version,string? publisher,string? location,DateTimeOffset? date,string? architecture,InstalledSoftwareSource source,string? externalId) { var now=DateTimeOffset.UtcNow; return new(0,name,version,publisher,normalizer.Normalize(name),normalizer.NormalizeVersion(version),location,date,architecture,source,externalId,null,null,null,now,now,true); }
    private static DateTimeOffset? ParseDate(string? value) => DateTime.TryParseExact(value,"yyyyMMdd",null,System.Globalization.DateTimeStyles.AssumeLocal,out var date) ? new DateTimeOffset(date) : null;
}
