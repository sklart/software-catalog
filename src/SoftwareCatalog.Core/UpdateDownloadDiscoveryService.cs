using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;
public sealed class UpdateDownloadDiscoveryService(IProductCatalogRepository repository, IEnumerable<IUpdateDownloadProvider> providers, IAppLogger? logger = null)
{
    public async Task<DownloadCandidateResolution> ResolveAsync(SoftwareProduct product, UpdateCheckResult? update, CancellationToken token)
    {
        var source = (await repository.GetUpdateSourcesAsync(product.Id, token)).Where(x => x.Enabled).OrderByDescending(x => x.IsExplicit).FirstOrDefault();
        if (source is null) return new(DownloadCandidateStatus.NotFound, [], "Источник обновления не настроен.");
        var provider = providers.FirstOrDefault(x => x.CanHandle(product, source));
        if (provider is null) return new(DownloadCandidateStatus.Unsupported, [], "Провайдер не поддерживает скачивание.");
        logger?.Information("download-resolution", $"Resolving {provider.Id} candidate for {product.Id}");
        try { return await provider.ResolveAsync(product, source, update, token); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { logger?.Error("download-resolution", ex.Message); return new(DownloadCandidateStatus.Error, [], ex.Message); }
    }
}
