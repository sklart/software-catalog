using System.Diagnostics;
using System.Text.Json;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Providers;

public interface IWinGetClient { Task<WinGetPackage?> ShowAsync(string packageId, CancellationToken cancellationToken); }
public sealed record WinGetPackage(string Id, string Name, string? Publisher, string Version);
public sealed class WinGetProvider(IWinGetClient client, ProductNormalizer normalizer) : IUpdateProvider
{
    public string Id => "WinGet";
    public bool CanHandle(SoftwareProduct product, ProductUpdateSource? source) => source?.Enabled == true && source.ProviderType.Equals(Id, StringComparison.OrdinalIgnoreCase);
    public async Task<UpdateCheckResult> CheckLatestAsync(SoftwareProduct product, ProductUpdateSource? source, CancellationToken token)
    {
        if (source is null) return new(UpdateStatus.NotFound);
        var package = await client.ShowAsync(source.ExternalId, token);
        if (package is null) return new(UpdateStatus.NotFound, Source: Id, ExternalProductId: source.ExternalId);
        if (normalizer.Normalize(package.Name) != product.NormalizedName || (!string.IsNullOrWhiteSpace(product.Publisher) && !string.IsNullOrWhiteSpace(package.Publisher) && normalizer.Normalize(product.Publisher) != normalizer.Normalize(package.Publisher))) return new(UpdateStatus.Ambiguous, Source: Id, ExternalProductId: source.ExternalId, Error: "WinGet package does not conclusively match product");
        return new(UpdateStatus.Unknown, package.Version, normalizer.NormalizeVersion(package.Version), package.Name, null, null, Id, package.Id);
    }
}
public sealed class ProcessWinGetClient : IWinGetClient
{
    public async Task<WinGetPackage?> ShowAsync(string packageId, CancellationToken token)
    {
        var start = new ProcessStartInfo("winget", $"show --id {packageId} --exact --accept-source-agreements") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("WinGet could not be started");
        var output = await process.StandardOutput.ReadToEndAsync(token); await process.WaitForExitAsync(token);
        if (process.ExitCode != 0) return null;
        string? name = null, version = null, publisher = null;
        foreach (var line in output.Split('\n')) { var item = line.Trim(); if (item.StartsWith("Name:", StringComparison.OrdinalIgnoreCase)) name = item[5..].Trim(); if (item.StartsWith("Version:", StringComparison.OrdinalIgnoreCase)) version = item[8..].Trim(); if (item.StartsWith("Publisher:", StringComparison.OrdinalIgnoreCase)) publisher = item[10..].Trim(); }
        return name is not null && version is not null ? new(packageId, name, publisher, version) : null;
    }
}
