using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Tests;

public sealed class UpdateDiscoveryServiceTests
{
    [Theory]
    [InlineData("1.0", "2.0", UpdateStatus.UpdateAvailable)]
    [InlineData("2.0", "2.0", UpdateStatus.UpToDate)]
    [InlineData("3.0", "2.0", UpdateStatus.LocalNewer)]
    [InlineData("bad", "2.0", UpdateStatus.Unknown)]
    public async Task MapsComparableVersionsAndPersists(string local, string remote, UpdateStatus expected)
    {
        var repository = new Repo(); var product = Product(local); repository.Sources.Add(Source(product)); var provider = new Provider(new(UpdateStatus.Unknown, remote, remote, ExternalProductId: "Vendor.Tool"));
        var result = await Service(repository, provider).CheckAsync(product, true, 12, CancellationToken.None);
        Assert.Equal(expected, result.Status); Assert.Equal(expected, repository.Saved!.Status); Assert.Equal(remote, repository.Saved.LatestVersion); Assert.Equal("WinGet", repository.Saved.Source);
    }
    [Fact] public async Task ReturnsProviderStatusesAndPreservesCancellation() { var repository = new Repo(); var product = Product("1.0"); repository.Sources.Add(Source(product)); foreach (var status in new[] { UpdateStatus.NotFound, UpdateStatus.Ambiguous, UpdateStatus.Error }) { var result = await Service(repository, new Provider(new(status))).CheckAsync(product, true, 12, CancellationToken.None); Assert.Equal(status, result.Status); } await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(repository, new Provider(null, true)).CheckAsync(product, true, 12, new CancellationToken(true))); }
    [Fact] public async Task HonorsCacheUnlessForced() { var repository = new Repo(); var product = Product("1.0") with { LastCheckedUtc = DateTimeOffset.UtcNow, UpdateStatus = UpdateStatus.UpToDate, LatestVersion = "1.0" }; repository.Sources.Add(Source(product)); var provider = new Provider(new(UpdateStatus.Unknown, "2.0", "2.0")); var service = Service(repository, provider); var cached = await service.CheckAsync(product, false, 12, CancellationToken.None); Assert.Equal(0, provider.Calls); Assert.Equal(UpdateStatus.UpToDate, cached.Status); await service.CheckAsync(product, true, 12, CancellationToken.None); Assert.Equal(1, provider.Calls); }
    [Fact] public async Task PrioritizesExplicitThenWinGet() { var repository = new Repo(); var product = Product("1.0"); repository.Sources.Add(new(Guid.NewGuid(), product.Id, "GitHub", "o/r", true, true)); repository.Sources.Add(Source(product)); var github = new Provider(new(UpdateStatus.Unknown, "2.0", "2.0"), id: "GitHub"); var winget = new Provider(new(UpdateStatus.Unknown, "9.0", "9.0")); await new UpdateDiscoveryService(repository, [winget, github], new VersionComparer()).CheckAsync(product, true, 12, CancellationToken.None); Assert.Equal(1, github.Calls); Assert.Equal(0, winget.Calls); }
    private static SoftwareProduct Product(string version) { var now = DateTimeOffset.UtcNow; return new(Guid.NewGuid(), "Tool", "Vendor", "tool", now, now, version, version); }
    private static ProductUpdateSource Source(SoftwareProduct product) => new(Guid.NewGuid(), product.Id, "WinGet", "Vendor.Tool", true, false);
    private static UpdateDiscoveryService Service(Repo repo, Provider provider) => new(repo, [provider], new VersionComparer());
    private sealed class Provider(UpdateCheckResult? result, bool cancel = false, string id = "WinGet") : IUpdateProvider { public int Calls; public string Id => id; public bool CanHandle(SoftwareProduct product, ProductUpdateSource? source) => source is not null && source.ProviderType == Id; public Task<UpdateCheckResult> CheckLatestAsync(SoftwareProduct product, ProductUpdateSource? source, CancellationToken token) { Calls++; if (cancel) return Task.FromCanceled<UpdateCheckResult>(token); return Task.FromResult(result!); } }
    private sealed class Repo : IProductCatalogRepository { public List<ProductUpdateSource> Sources { get; } = []; public UpdateCheckResult? Saved; public Task<IReadOnlyList<SoftwareProduct>> GetProductsAsync(CancellationToken t) => Task.FromResult<IReadOnlyList<SoftwareProduct>>([]); public Task<IReadOnlyList<InstallerFile>> GetInstallersForProductAsync(Guid p, CancellationToken t) => Task.FromResult<IReadOnlyList<InstallerFile>>([]); public Task<SoftwareProduct> UpsertProductAsync(SoftwareProduct p, CancellationToken t) => Task.FromResult(p); public Task LinkInstallerAsync(long i, Guid p, ProductMatchSource s, ProductMatchConfidence c, CancellationToken t) => Task.CompletedTask; public Task<IReadOnlyList<ProductUpdateSource>> GetUpdateSourcesAsync(Guid p, CancellationToken t) => Task.FromResult<IReadOnlyList<ProductUpdateSource>>(Sources.Where(s => s.ProductId == p).ToArray()); public Task SetUpdateSourceAsync(ProductUpdateSource s, CancellationToken t) { Sources.Add(s); return Task.CompletedTask; } public Task ClearUpdateSourcesAsync(Guid p, string type, CancellationToken t) => Task.CompletedTask; public Task SaveUpdateCheckAsync(Guid p, UpdateCheckResult r, CancellationToken t) { Saved = r; return Task.CompletedTask; } }
}
