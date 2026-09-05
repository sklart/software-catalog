using System.Diagnostics;
using System.Text.Json;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Providers;

public interface IWinGetClient { Task<WinGetPackage?> ShowAsync(string packageId, CancellationToken cancellationToken); Task<IReadOnlyList<WinGetPackage>> SearchAsync(string name, CancellationToken cancellationToken); Task<IReadOnlyList<WinGetInstaller>> GetInstallersAsync(string packageId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WinGetInstaller>>([]); }
public sealed record WinGetPackage(string Id, string Name, string? Publisher, string Version);
public sealed record WinGetInstaller(string? InstallerUrl, string? InstallerSha256, string? Architecture, string? InstallerType, string? Scope = null, string? Locale = null);
public sealed class WinGetProvider(IWinGetClient client, ProductNormalizer normalizer) : IUpdateProvider, IUpdateDownloadProvider
{
    public string Id => "WinGet";
    public bool CanHandle(SoftwareProduct product, ProductUpdateSource? source) => source is null || (source.Enabled && source.ProviderType.Equals(Id, StringComparison.OrdinalIgnoreCase));
    public async Task<UpdateCheckResult> CheckLatestAsync(SoftwareProduct product, ProductUpdateSource? source, CancellationToken token)
    {
        if (source is null)
        {
            var candidates = await client.SearchAsync(product.CanonicalName, token);
            var enriched = await Task.WhenAll(candidates.Where(candidate => normalizer.Normalize(candidate.Name) == product.NormalizedName).Select(async candidate => await client.ShowAsync(candidate.Id, token) ?? candidate));
            var matches = enriched.Where(candidate => !string.IsNullOrWhiteSpace(candidate.Name) && normalizer.Normalize(candidate.Name) == product.NormalizedName && (string.IsNullOrWhiteSpace(product.Publisher) || (!string.IsNullOrWhiteSpace(candidate.Publisher) && normalizer.Normalize(candidate.Publisher) == normalizer.Normalize(product.Publisher)))).ToArray();
            if (matches.Length == 0) return new(UpdateStatus.NotFound, Source: Id);
            if (matches.Length != 1) return new(UpdateStatus.Ambiguous, Source: Id, Error: "Multiple plausible WinGet packages");
            var candidate = matches[0];
            return new(UpdateStatus.Unknown, candidate.Version, normalizer.NormalizeVersion(candidate.Version), candidate.Name, null, null, Id, candidate.Id);
        }
        var package = await client.ShowAsync(source.ExternalId, token);
        if (package is null) return new(UpdateStatus.NotFound, Source: Id, ExternalProductId: source.ExternalId);
        if (normalizer.Normalize(package.Name) != product.NormalizedName || (!string.IsNullOrWhiteSpace(product.Publisher) && !string.IsNullOrWhiteSpace(package.Publisher) && normalizer.Normalize(product.Publisher) != normalizer.Normalize(package.Publisher))) return new(UpdateStatus.Ambiguous, Source: Id, ExternalProductId: source.ExternalId, Error: "WinGet package does not conclusively match product");
        return new(UpdateStatus.Unknown, package.Version, normalizer.NormalizeVersion(package.Version), package.Name, null, null, Id, package.Id);
    }
    public async Task<DownloadCandidateResolution> ResolveAsync(SoftwareProduct product, ProductUpdateSource source, UpdateCheckResult? update, CancellationToken token)
    {
        var package = await client.ShowAsync(source.ExternalId, token); if (package is null) return new(DownloadCandidateStatus.NotFound, []);
        var all = await client.GetInstallersAsync(source.ExternalId, token);
        var candidates = all.Where(x => Uri.TryCreate(x.InstallerUrl, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps).Select(x => { var uri = new Uri(x.InstallerUrl!); return new DownloadCandidate(Id, source.ExternalId, update?.LatestVersion ?? package.Version, normalizer.NormalizeVersion(update?.LatestVersion ?? package.Version), Path.GetFileName(uri.AbsolutePath), uri, null, null, x.Architecture, x.InstallerType, x.InstallerSha256, x.InstallerSha256 is null ? null : "WinGet manifest"); }).ToArray();
        if (candidates.Length == 0) return new(DownloadCandidateStatus.NotFound, [], "WinGet manifest has no HTTPS installer URL.");
        return candidates.Length == 1 ? new(DownloadCandidateStatus.Available, candidates) : new(DownloadCandidateStatus.Ambiguous, candidates, "Несколько подходящих installer-файлов.");
    }
}
public sealed class ProcessWinGetClient : IWinGetClient
{
    public async Task<IReadOnlyList<WinGetPackage>> SearchAsync(string name, CancellationToken token)
    {
        var start = new ProcessStartInfo("winget", $"search --name \"{name.Replace("\"", string.Empty)}\" --accept-source-agreements") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("WinGet could not be started");
        var output = await process.StandardOutput.ReadToEndAsync(token); await process.WaitForExitAsync(token);
        if (process.ExitCode != 0) return [];
        return WinGetSearchParser.Parse(output);
    }
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
    public async Task<IReadOnlyList<WinGetInstaller>> GetInstallersAsync(string packageId, CancellationToken token)
    {
        // `show --manifest` is read-only and prints the selected installer manifest; it never invokes installation.
        var start = new ProcessStartInfo("winget", $"show --id {packageId} --exact --manifest --accept-source-agreements") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("WinGet could not be started");
        var output = await process.StandardOutput.ReadToEndAsync(token); await process.WaitForExitAsync(token);
        return process.ExitCode == 0 ? WinGetManifestParser.Parse(output) : [];
    }
}

public static class WinGetManifestParser
{
    public static IReadOnlyList<WinGetInstaller> Parse(string text)
    {
        var result = new List<WinGetInstaller>(); var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        void Flush() { if (values.TryGetValue("InstallerUrl", out var url) && !string.IsNullOrWhiteSpace(url)) { values.TryGetValue("InstallerSha256", out var hash); values.TryGetValue("Architecture", out var architecture); values.TryGetValue("InstallerType", out var type); values.TryGetValue("Scope", out var scope); values.TryGetValue("InstallerLocale", out var locale); result.Add(new(url, hash, architecture, type, scope, locale)); } values.Clear(); }
        foreach (var raw in text.Split('\n')) { var line = raw.Trim(); if (line.StartsWith("-", StringComparison.Ordinal) && values.Count > 0) Flush(); var separator = line.IndexOf(':'); if (separator <= 0) continue; var key = line[..separator].TrimStart('-', ' ').Trim(); if (key is "InstallerUrl" or "InstallerSha256" or "Architecture" or "InstallerType" or "Scope" or "InstallerLocale") values[key] = line[(separator + 1)..].Trim(); }
        Flush(); return result;
    }
}
