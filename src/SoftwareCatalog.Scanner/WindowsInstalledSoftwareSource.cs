using Microsoft.Win32;
using System.Runtime.Versioning;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Scanner;

/// <summary>Reads Windows inventory only; it never invokes installers, package managers, or shell commands.</summary>
[SupportedOSPlatform("windows")]
public class WindowsInstalledSoftwareSource(ProductNormalizer normalizer) : IInstalledSoftwareSource
{
    private const string Uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    public Task<IReadOnlyList<InstalledSoftware>> ReadAsync(CancellationToken token)
    {
        var result = new List<InstalledSoftware>();
        TryRead(() => ReadUninstall(RegistryHive.LocalMachine, RegistryView.Registry64, InstalledSoftwareSource.Hklm64Uninstall, "x64", result, token), token);
        TryRead(() => ReadUninstall(RegistryHive.LocalMachine, RegistryView.Registry32, InstalledSoftwareSource.Hklm32Uninstall, "x86", result, token), token);
        TryRead(() => ReadUninstall(RegistryHive.CurrentUser, RegistryView.Default, InstalledSoftwareSource.HkcuUninstall, null, result, token), token);
        TryRead(() => ReadMsix(result, token), token);
        return Task.FromResult<IReadOnlyList<InstalledSoftware>>(result);
    }
    protected virtual void ReadUninstall(RegistryHive hive, RegistryView view, InstalledSoftwareSource source, string? defaultArchitecture, List<InstalledSoftware> result, CancellationToken token)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view); using var uninstall = baseKey.OpenSubKey(Uninstall, false); if (uninstall is null) return;
        foreach (var name in uninstall.GetSubKeyNames()) { token.ThrowIfCancellationRequested(); using var key=uninstall.OpenSubKey(name, false); if (key is null) continue; var display=key.GetValue("DisplayName") as string; if(string.IsNullOrWhiteSpace(display)) continue; var version=key.GetValue("DisplayVersion") as string; var publisher=key.GetValue("Publisher") as string; var location=key.GetValue("InstallLocation") as string; var date=ParseDate(key.GetValue("InstallDate") as string); var architecture=(key.GetValue("Architecture") as string) ?? defaultArchitecture; var itemSource=Convert.ToInt32(key.GetValue("WindowsInstaller",0)) == 1 ? InstalledSoftwareSource.Msi : source; result.Add(New(display,version,publisher,location,date,architecture,itemSource,name)); }
    }
    protected virtual void ReadMsix(List<InstalledSoftware> result, CancellationToken token)
    {
        // Package repository registry is readable without PowerShell and contains only registered packages.
        using var baseKey=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64); using var packages=baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel\StateRepository\Cache\Package\Data",false); if(packages is null)return;
        foreach(var family in packages.GetSubKeyNames()) { token.ThrowIfCancellationRequested(); using var key=packages.OpenSubKey(family,false); var full=key?.GetValue("PackageFullName") as string; if(string.IsNullOrWhiteSpace(full))continue; var parts=full.Split('_'); if(parts.Length<2)continue; var architecture=parts.Length > 2 && new[]{"x86","x64","arm","arm64","neutral"}.Contains(parts[2],StringComparer.OrdinalIgnoreCase) ? parts[2] : null; result.Add(New(parts[0],parts[1],null,null,null,architecture,InstalledSoftwareSource.Msix,family)); }
    }
    private InstalledSoftware New(string name,string? version,string? publisher,string? location,DateTimeOffset? date,string? architecture,InstalledSoftwareSource source,string? externalId) { var now=DateTimeOffset.UtcNow; return new(0,name,version,publisher,normalizer.Normalize(name),normalizer.NormalizeVersion(version),location,date,architecture,source,externalId,null,null,null,now,now,true); }
    private static void TryRead(Action read, CancellationToken token) { try { read(); } catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; } catch (Exception) { } }
    private static DateTimeOffset? ParseDate(string? value) => DateTime.TryParseExact(value,"yyyyMMdd",null,System.Globalization.DateTimeStyles.AssumeLocal,out var date) ? new DateTimeOffset(date) : null;
}
