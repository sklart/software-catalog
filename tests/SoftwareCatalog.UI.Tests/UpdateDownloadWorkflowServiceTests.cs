using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Database;
using SoftwareCatalog.Infrastructure.Paths;
using SoftwareCatalog.Infrastructure.Settings;
using SoftwareCatalog.Scanner;
using SoftwareCatalog.UI;

namespace SoftwareCatalog.UI.Tests;

public sealed class UpdateDownloadWorkflowServiceTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SoftwareCatalogWorkflowTests", Guid.NewGuid().ToString("N"));
    private CatalogDatabase _database = null!;
    private Paths _paths = null!;
    public async Task InitializeAsync() { Directory.CreateDirectory(_root); _paths = new Paths(_root); _paths.EnsureDirectories(); _database = new CatalogDatabase(_paths.DatabasePath); await _database.InitializeAsync(CancellationToken.None); }
    public Task DisposeAsync() { if (Directory.Exists(_root)) Directory.Delete(_root, true); return Task.CompletedTask; }

    [Fact]
    public async Task ValidDownloadIsScannedRegroupedAndRecorded()
    {
        var product = await AddProductAsync("2.0"); var payload = Msix("Tool", "Vendor", "2.0", "Contoso.Tool"); await SeedKnownInstallerAsync(product, "Contoso.Tool", "1.0");
        var result = await Workflow(payload).ExecuteAsync(product, Candidate("Tool-2.0.msix"), null, CancellationToken.None);
        Assert.Equal(DownloadStatus.Completed, result.Result.Status); Assert.Single(await _database.GetInstallersForProductAsync(product.Id, CancellationToken.None), file => file.ProductVersion == "2.0");
        Assert.Equal("2.0", (await _database.GetProductsAsync(CancellationToken.None)).Single(item => item.Id == product.Id).LatestLocalVersion);
        Assert.Equal(DownloadStatus.Completed, Assert.Single(await ((IDownloadHistoryRepository)_database).GetDownloadHistoryAsync(CancellationToken.None)).Status);
    }

    [Fact]
    public async Task DuplicateHashDoesNotImportAnotherInstallerButRecordsHistory()
    {
        var product = await AddProductAsync("2.0"); var payload = Msix("Tool", "Vendor", "2.0", "Contoso.Tool"); await SeedKnownInstallerAsync(product, "Contoso.Tool", "1.0", Convert.ToHexString(SHA256.HashData(payload)));
        var result = await Workflow(payload).ExecuteAsync(product, Candidate("Tool-2.0.msix"), null, CancellationToken.None);
        Assert.Equal(DownloadStatus.AlreadyExists, result.Result.Status); Assert.Single(await _database.GetInstallersAsync(CancellationToken.None)); Assert.Equal(DownloadStatus.AlreadyExists, Assert.Single(await ((IDownloadHistoryRepository)_database).GetDownloadHistoryAsync(CancellationToken.None)).Status);
    }

    [Fact]
    public async Task MismatchAndOlderVersionAreRejectedWithoutImport()
    {
        var product = await AddProductAsync("2.0"); await SeedKnownInstallerAsync(product, "Contoso.Tool", "1.0");
        var mismatch = await Workflow(Msix("Tool", "Vendor", "2.0", "Other.App")).ExecuteAsync(product, Candidate("Other-2.0.msix"), null, CancellationToken.None);
        Assert.Equal(DownloadStatus.ValidationFailed, mismatch.Result.Status);
        var older = await Workflow(Msix("Tool", "Vendor", "1.0", "Contoso.Tool")).ExecuteAsync(product, Candidate("Tool-1.0.msix"), null, CancellationToken.None);
        Assert.Equal(DownloadStatus.ValidationFailed, older.Result.Status); Assert.Single(await _database.GetInstallersAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UnknownMetadataRequiresConfirmationThenImports()
    {
        var product = await AddProductAsync("2.0"); var workflow = Workflow(Encoding.UTF8.GetBytes("not a portable executable"));
        var initial = await workflow.ExecuteAsync(product, Candidate("unknown.exe"), null, CancellationToken.None);
        Assert.Equal(DownloadStatus.ManualConfirmationRequired, initial.Result.Status); Assert.Empty(await _database.GetInstallersAsync(CancellationToken.None));
        var confirmed = await workflow.ConfirmManualImportAsync(product, initial.Candidate, initial.Result, CancellationToken.None);
        Assert.Equal(DownloadStatus.Completed, confirmed.Status); Assert.Single(await _database.GetInstallersAsync(CancellationToken.None));
        var history = await ((IDownloadHistoryRepository)_database).GetDownloadHistoryAsync(CancellationToken.None); Assert.Contains(history, row => row.Status == DownloadStatus.ManualConfirmationRequired); Assert.Contains(history, row => row.Status == DownloadStatus.Completed);
    }

    [Fact]
    public async Task CancellationLeavesNoFinalInstallerAndRecordsCancelledHistory()
    {
        var product = await AddProductAsync("2.0"); using var cancel = new CancellationTokenSource(); var workflow = Workflow([1], delay: true); var task = workflow.ExecuteAsync(product, Candidate("Tool-2.0.exe"), null, cancel.Token); await Task.Delay(30); cancel.Cancel(); var result = await task;
        Assert.Equal(DownloadStatus.Cancelled, result.Result.Status); Assert.Empty(await _database.GetInstallersAsync(CancellationToken.None)); Assert.Equal(DownloadStatus.Cancelled, Assert.Single(await ((IDownloadHistoryRepository)_database).GetDownloadHistoryAsync(CancellationToken.None)).Status);
    }
    [Fact]
    public async Task MatchingUpgradeCodeAllowsNewMsiProductCode()
    {
        var product = await AddProductAsync("2.0"); await SeedKnownInstallerAsync(product, null, "1.0", kind: InstallerKind.Msi, productCode: "OLD-PRODUCT", upgradeCode: "SAME-UPGRADE");
        var metadata = MsiMetadata("Tool", "Vendor", "2.0", "NEW-PRODUCT", "SAME-UPGRADE"); var result = await Workflow([1], metadata).ExecuteAsync(product, Candidate("Tool-2.0.msi"), null, CancellationToken.None);
        Assert.Equal(DownloadStatus.Completed, result.Result.Status); Assert.True(File.Exists(result.Result.FinalPath));
    }
    [Fact]
    public async Task MatchingProductCodeAllowsMsiImport()
    {
        var product = await AddProductAsync("2.0"); await SeedKnownInstallerAsync(product, null, "1.0", kind: InstallerKind.Msi, productCode: "SAME-PRODUCT");
        var result = await Workflow([1], MsiMetadata("Tool", "Vendor", "2.0", "SAME-PRODUCT", null)).ExecuteAsync(product, Candidate("Tool-2.0.msi"), null, CancellationToken.None);
        Assert.Equal(DownloadStatus.Completed, result.Result.Status);
    }
    [Fact]
    public async Task NameAndPublisherAllowsImportButDifferentPublisherRejectsIt()
    {
        var product = await AddProductAsync("2.0"); await SeedKnownInstallerAsync(product, null, "1.0", kind: InstallerKind.Msi);
        var accepted = await Workflow([1], MsiMetadata("Tool", "Vendor", "2.0", null, null)).ExecuteAsync(product, Candidate("Tool-2.0.msi"), null, CancellationToken.None); Assert.Equal(DownloadStatus.Completed, accepted.Result.Status);
        var rejected = await Workflow([2], MsiMetadata("Tool", "Other Vendor", "2.0", null, null)).ExecuteAsync(product, Candidate("Other-2.0.msi"), null, CancellationToken.None); Assert.Equal(DownloadStatus.ValidationFailed, rejected.Result.Status); Assert.False(File.Exists(Path.Combine(_root, "Downloads", "Other-2.0.msi")));
    }
    [Fact]
    public async Task ConflictingMsiStableIdentifierRejectsNamePublisherFallback()
    {
        var product = await AddProductAsync("2.0"); await SeedKnownInstallerAsync(product, null, "1.0", kind: InstallerKind.Msi, productCode: "OLD-PRODUCT", upgradeCode: "OLD-UPGRADE");
        var result = await Workflow([1], MsiMetadata("Tool", "Vendor", "2.0", "NEW-PRODUCT", "NEW-UPGRADE")).ExecuteAsync(product, Candidate("Tool-2.0.msi"), null, CancellationToken.None);
        Assert.Equal(DownloadStatus.ValidationFailed, result.Result.Status); Assert.False(File.Exists(Path.Combine(_root, "Downloads", "Tool-2.0.msi")));
    }

    private UpdateDownloadWorkflowService Workflow(byte[] content, InstallerMetadata? metadata = null, bool delay = false)
    {
        var scanner = new CatalogScanner(_database, new PortablePathResolver(_paths), new FileHashCalculator(), new CatalogScannerOptions { MaxDegreeOfParallelism = 1 });
        var catalog = new ProductCatalogService(_database, _database, new ProductNormalizer(), new ProductMatchingService(new ProductNormalizer()), new VersionComparer());
        return new UpdateDownloadWorkflowService(new DownloadService(new HttpClient(new ContentHandler(content, delay))), new DownloadCoordinator(1), _database, _database, _database, scanner, catalog, _paths, AppSettings.Default with { DownloadDestination = "Downloads" }, new ProductMatchingService(new ProductNormalizer()), metadataService: metadata is null ? null : new InstallerMetadataService([new FixedMetadataExtractor(metadata)]));
    }
    private async Task<SoftwareProduct> AddProductAsync(string latest) { var now = DateTimeOffset.UtcNow; return await _database.UpsertProductAsync(new SoftwareProduct(Guid.NewGuid(), "Tool", "Vendor", "tool", now, now, latest, latest, UpdateStatus.UpdateAvailable, latest), CancellationToken.None); }
    private async Task SeedKnownInstallerAsync(SoftwareProduct product, string? identity, string version, string? hash = null, InstallerKind kind = InstallerKind.Msix, string? productCode = null, string? upgradeCode = null) { var root = await _database.AddScanRootAsync("Known", ScanRootPathKind.RelativeToApplication, false, CancellationToken.None); var now = DateTimeOffset.UtcNow; var extension = kind == InstallerKind.Msi ? ".msi" : ".msix"; await _database.UpsertInstallersAsync([new InstallerFile(0, root.Id, "known" + extension, "known" + extension, extension, 1, now, hash, now, now, true, kind, "Tool", version, "Vendor", null, null, "x64", MetadataSource.MsixManifest, MetadataStatus.Success, null, version, productCode, upgradeCode, MsixIdentityName: identity)], CancellationToken.None); var known = Assert.Single(await _database.GetInstallersAsync(CancellationToken.None), file => file.ScanRootId == root.Id); await _database.LinkInstallerAsync(known.Id, product.Id, identity is null ? ProductMatchSource.NameAndPublisher : ProductMatchSource.MsixIdentity, ProductMatchConfidence.High, CancellationToken.None); }
    private static DownloadCandidate Candidate(string name) => new("Test", "Tool", "2.0", "2.0", name, new Uri("https://example.test/" + name));
    private static InstallerMetadata MsiMetadata(string name, string publisher, string version, string? productCode, string? upgradeCode) => new(InstallerKind.Msi, name, version, publisher, Source: MetadataSource.MsiDatabase, Status: MetadataStatus.Success, ProductCode: productCode, UpgradeCode: upgradeCode);
    private static byte[] Msix(string name, string publisher, string version, string identity) { using var stream = new MemoryStream(); using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true)) { var entry = archive.CreateEntry("AppxManifest.xml"); using var writer = new StreamWriter(entry.Open()); writer.Write($"<Package xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\"><Identity Name=\"{identity}\" Publisher=\"CN=Vendor\" Version=\"{version}\" ProcessorArchitecture=\"x64\"/><Properties><DisplayName>{name}</DisplayName><PublisherDisplayName>{publisher}</PublisherDisplayName></Properties></Package>"); } return stream.ToArray(); }
    private sealed class ContentHandler(byte[] content, bool delay) : HttpMessageHandler { protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { if (delay) await Task.Delay(Timeout.InfiniteTimeSpan, token); return new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) }; } }
    private sealed class FixedMetadataExtractor(InstallerMetadata metadata) : IInstallerMetadataExtractor { public bool CanExtract(InstallerKind kind) => true; public InstallerMetadata Extract(string path, InstallerKind kind) => metadata with { Kind = kind }; }
    private sealed class Paths(string root) : IAppPathService { public string ApplicationRoot => root; public string DataDirectory => Path.Combine(root, "data"); public string DatabasePath => Path.Combine(DataDirectory, "catalog.db"); public string ConfigDirectory => Path.Combine(root, "config"); public string SettingsPath => Path.Combine(ConfigDirectory, "settings.json"); public string LogsDirectory => Path.Combine(root, "logs"); public string CacheDirectory => Path.Combine(root, "cache"); public string BackupsDirectory => Path.Combine(root, "backups"); public void EnsureDirectories() { foreach (var path in new[] { ApplicationRoot, DataDirectory, ConfigDirectory, LogsDirectory, CacheDirectory, BackupsDirectory }) Directory.CreateDirectory(path); } }
}
