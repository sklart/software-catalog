using System.IO;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Infrastructure.Settings;
using SoftwareCatalog.Infrastructure.Paths;
using SoftwareCatalog.Scanner;

namespace SoftwareCatalog.UI;

/// <summary>Owns the download/import transaction; UI only supplies a candidate and observes progress.</summary>
public sealed class UpdateDownloadWorkflowService(DownloadService downloader, DownloadCoordinator coordinator, IDownloadHistoryRepository history, IProductCatalogRepository products, IScanCatalogRepository scans, CatalogScanner scanner, ProductCatalogService catalog, IAppPathService appPaths, AppSettings settings, ProductMatchingService matching, IAppLogger? logger = null)
{
    public async Task<DownloadWorkflowResult> ExecuteAsync(SoftwareProduct product, DownloadCandidate candidate, IProgress<DownloadProgress>? progress, CancellationToken token)
        => await coordinator.RunAsync(inner => ExecuteCoreAsync(product, candidate, progress, inner), token);
    private async Task<DownloadWorkflowResult> ExecuteCoreAsync(SoftwareProduct product, DownloadCandidate candidate, IProgress<DownloadProgress>? progress, CancellationToken token)
    {
        var started = DateTimeOffset.UtcNow; DownloadResult result;
        try
        {
            var destination = DownloadDestinationResolver.Resolve(settings, appPaths);
            logger?.Information("download", $"Download started product={product.Id} provider={candidate.Provider} file={candidate.FileName}"); result = await downloader.DownloadAsync(candidate, Path.Combine(appPaths.CacheDirectory, "Staging"), destination, progress, TimeSpan.FromMinutes(settings.DownloadTimeoutMinutes), token);
            if (result.Status == DownloadStatus.Completed && result.FinalPath is not null && result.Sha256 is not null)
            {
                if (await history.HasInstallerSha256Async(result.Sha256, token)) { logger?.Information("download", "Duplicate SHA-256 detected."); File.Delete(result.FinalPath); result = new(DownloadStatus.AlreadyExists, null, result.Sha256, "Такой SHA-256 уже есть в каталоге."); }
                else { var validation = await ValidateAsync(product, result.FinalPath, token); result = validation == DownloadStatus.Completed ? await ImportAsync(candidate, result, destination, token) : new(validation, result.FinalPath, result.Sha256, validation == DownloadStatus.ManualConfirmationRequired ? "Недостаточно metadata для автоматического импорта." : "Installer не соответствует ожидаемому продукту или версии."); }
            }
        }
        catch (OperationCanceledException) { logger?.Information("download", "Download cancelled."); result = new(DownloadStatus.Cancelled, null, null, "Отменено."); }
        catch (Exception ex) { logger?.Error("download", ex.Message); result = new(DownloadStatus.Error, null, null, ex.Message); }
        await history.SaveDownloadHistoryAsync(new(Guid.NewGuid(), product.Id, candidate.Provider, candidate.ExternalProductId, candidate.Version, candidate.FileName, candidate.Uri?.ToString(), candidate.Sha256, result.Sha256, result.Status, result.Error, started, DateTimeOffset.UtcNow, result.FinalPath is null ? null : Path.GetRelativePath(appPaths.ApplicationRoot, result.FinalPath)), CancellationToken.None); return new(result, candidate);
    }
    public async Task<DownloadResult> ConfirmManualImportAsync(SoftwareProduct product, DownloadCandidate candidate, DownloadResult staged, CancellationToken token)
    {
        if (staged.Status != DownloadStatus.ManualConfirmationRequired || staged.FinalPath is null || staged.Sha256 is null) return staged;
        var destination = DownloadDestinationResolver.Resolve(settings, appPaths);
        var result = await ImportAsync(candidate, staged, destination, token);
        await history.SaveDownloadHistoryAsync(new(Guid.NewGuid(), product.Id, candidate.Provider, candidate.ExternalProductId, candidate.Version, candidate.FileName, candidate.Uri?.ToString(), candidate.Sha256, result.Sha256, result.Status, result.Error, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, result.FinalPath is null ? null : Path.GetRelativePath(appPaths.ApplicationRoot, result.FinalPath)), CancellationToken.None);
        return result;
    }
    private async Task<DownloadStatus> ValidateAsync(SoftwareProduct product, string stagedPath, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var metadata = new InstallerMetadataService().Extract(stagedPath);
        if (metadata.Status != MetadataStatus.Success || string.IsNullOrWhiteSpace(metadata.ProductName) || string.IsNullOrWhiteSpace(metadata.ProductVersion)) { logger?.Information("download", "Manual confirmation required: metadata incomplete."); return DownloadStatus.ManualConfirmationRequired; }
        var now = DateTimeOffset.UtcNow; var probe = new InstallerFile(0, 0, Path.GetFileName(stagedPath), Path.GetFileName(stagedPath), Path.GetExtension(stagedPath), new FileInfo(stagedPath).Length, now, null, now, now, true, metadata.Kind, metadata.ProductName, metadata.ProductVersion, metadata.Publisher, metadata.FileVersion, metadata.FileDescription, metadata.Architecture, metadata.Source, metadata.Status, metadata.Error, VersionNormalizer.Normalize(metadata.ProductVersion), metadata.ProductCode, metadata.UpgradeCode, metadata.PackageList, MsixIdentityName: metadata.MsixIdentityName);
        var known = await products.GetInstallersForProductAsync(product.Id, token); var match = matching.FindMatch(probe, known);
        var normalizer = new ProductNormalizer(); var nameMatch = normalizer.Normalize(metadata.ProductName) == product.NormalizedName && (string.IsNullOrWhiteSpace(product.Publisher) || normalizer.Normalize(metadata.Publisher) == normalizer.Normalize(product.Publisher));
        if (match?.ProductId != product.Id && !nameMatch) { logger?.Error("download", "Validation failed: product mismatch."); return DownloadStatus.ValidationFailed; }
        if (!string.IsNullOrWhiteSpace(product.LatestNormalizedVersion) && new VersionComparer().Compare(probe.NormalizedVersion, product.LatestNormalizedVersion) == VersionComparisonResult.Older) { logger?.Error("download", "Validation failed: older version."); return DownloadStatus.ValidationFailed; }
        return DownloadStatus.Completed;
    }
    private async Task<DownloadResult> ImportAsync(DownloadCandidate candidate, DownloadResult staged, string destination, CancellationToken token)
    {
        var finalized = await DownloadService.FinalizeAsync(staged.FinalPath!, destination, candidate.FileName ?? "download.bin", staged.Sha256!, token); if (finalized.Status != DownloadStatus.Completed) return finalized;
        var root = (await scans.GetScanRootsAsync(token)).FirstOrDefault(x => new SoftwareCatalog.Infrastructure.Paths.PortablePathResolver(appPaths).Resolve(x).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase));
        if (root is null) root = await scans.AddScanRootAsync(settings.DownloadDestinationKind == DownloadDestinationKind.Absolute ? destination : settings.DownloadDestination, settings.DownloadDestinationKind == DownloadDestinationKind.Absolute ? ScanRootPathKind.Absolute : ScanRootPathKind.RelativeToApplication, false, token);
        await scanner.ScanAsync(root, null, token); await catalog.RegroupAsync(token); logger?.Information("download", "Catalog import completed."); return finalized;
    }
}
public sealed record DownloadWorkflowResult(DownloadResult Result, DownloadCandidate Candidate);
