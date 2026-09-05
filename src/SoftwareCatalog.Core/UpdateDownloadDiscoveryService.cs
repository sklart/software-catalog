using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;
public sealed class UpdateDownloadDiscoveryService(IProductCatalogRepository repository, IEnumerable<IUpdateDownloadProvider> providers, IAppLogger? logger = null)
{
    public async Task<DownloadCandidateResolution> ResolveAsync(SoftwareProduct product, UpdateCheckResult? update, CancellationToken token)
    {
        DownloadCandidateResolution Complete(DownloadCandidateResolution value) { logger?.Information("download-resolution", $"Resolution completed status={value.Status} candidates={value.Candidates.Count}"); return value; }
        var source = (await repository.GetUpdateSourcesAsync(product.Id, token)).Where(x => x.Enabled).OrderByDescending(x => x.IsExplicit).FirstOrDefault();
        if (source is null) return Complete(new(DownloadCandidateStatus.NotFound, [], "Источник обновления не настроен."));
        var provider = providers.FirstOrDefault(x => x.CanHandle(product, source));
        if (provider is null) return Complete(new(DownloadCandidateStatus.Unsupported, [], "Провайдер не поддерживает скачивание."));
        logger?.Information("download-resolution", $"Resolving {provider.Id} candidate for {product.Id}");
        try
        {
            var result = await provider.ResolveAsync(product, source, update, token);
            if (result.Status is DownloadCandidateStatus.Error or DownloadCandidateStatus.NotFound or DownloadCandidateStatus.Unsupported) return Complete(result);
            var architectures = (await repository.GetInstallersForProductAsync(product.Id, token)).Select(x => NormalizeArchitecture(x.Architecture)).Where(x => x is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (architectures.Length != 1) return Complete(result.Candidates.Count == 1 ? result with { Status = DownloadCandidateStatus.Available } : result with { Status = DownloadCandidateStatus.Ambiguous, Error = "Недостаточно сведений об архитектуре локального продукта." });
            var requested = architectures[0]!;
            var exact = result.Candidates.Where(x => NormalizeArchitecture(x.Architecture)?.Equals(requested, StringComparison.OrdinalIgnoreCase) == true).ToArray();
            var neutral = result.Candidates.Where(x => NormalizeArchitecture(x.Architecture) is null or "neutral").ToArray();
            var chosen = exact.Length > 0 ? exact : neutral;
            return Complete(chosen.Length switch { 0 => new(DownloadCandidateStatus.Unsupported, [], "Нет совместимого installer для архитектуры продукта."), 1 => new(DownloadCandidateStatus.Available, chosen), _ => new(DownloadCandidateStatus.Ambiguous, chosen, "Несколько равноправных installer-файлов.") });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { logger?.Error("download-resolution", ex.Message); return Complete(new(DownloadCandidateStatus.Error, [], ex.Message)); }
    }
    private static string? NormalizeArchitecture(string? architecture) => architecture?.Trim().ToLowerInvariant() switch { "amd64" or "win64" => "x64", "win32" => "x86", "universal" => "neutral", "arm" => "arm64", "x64" or "x86" or "arm64" or "neutral" => architecture.Trim().ToLowerInvariant(), _ => null };
}
