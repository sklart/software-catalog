using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Database;

namespace SoftwareCatalog.Database.Tests;
public sealed class CatalogDatabaseTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private CatalogDatabase _database = null!;
    public async Task InitializeAsync() { Directory.CreateDirectory(_folder); _database = new CatalogDatabase(Path.Combine(_folder, "catalog.db")); await _database.InitializeAsync(CancellationToken.None); }
    public Task DisposeAsync() { Directory.Delete(_folder, true); return Task.CompletedTask; }
    [Fact] public async Task PersistsRootAndInstallerAndMarksMissing()
    {
        var root = await _database.AddScanRootAsync("..\\Software", ScanRootPathKind.RelativeToApplication, true, CancellationToken.None);
        var now = DateTimeOffset.UtcNow; await _database.UpsertInstallersAsync([new(0, root.Id, "tool.exe", "tool.exe", ".exe", 1, now, "A", now, now, true)], CancellationToken.None);
        Assert.Single(await _database.GetInstallersAsync(CancellationToken.None)); await _database.MarkMissingAsync(root.Id, now.AddSeconds(1), CancellationToken.None);
        Assert.False((await _database.GetInstallersAsync(CancellationToken.None)).Single().Exists);
    }
    [Fact] public async Task CaseOnlyPathIsUpdatedInsteadOfDuplicated()
    {
        var root = await _database.AddScanRootAsync("C:\\Software", ScanRootPathKind.Absolute, true, CancellationToken.None); var now = DateTimeOffset.UtcNow;
        await _database.UpsertInstallersAsync([new(0, root.Id, "Tool.EXE", "Tool.EXE", ".exe", 1, now, "A", now, now, true)], CancellationToken.None);
        await _database.UpsertInstallersAsync([new(0, root.Id, "tool.exe", "tool.exe", ".exe", 2, now, "B", now, now, true)], CancellationToken.None);
        var file = (await _database.GetInstallersAsync(CancellationToken.None)).Single(); Assert.Equal(2, file.Size); Assert.Equal("B", file.Sha256);
    }
}
