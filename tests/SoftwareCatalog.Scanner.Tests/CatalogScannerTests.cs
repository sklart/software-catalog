using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Scanner.Tests;

public sealed class CatalogScannerTests
{
    [Fact]
    public async Task ScanAsync_HashesSupportedFileAndSkipsUnsupportedFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(folder, "setup.exe"), "safe test content");
            await File.WriteAllTextAsync(Path.Combine(folder, "readme.txt"), "ignored");
            var repository = new MemoryRepository();
            var scanner = new CatalogScanner(repository, new FileHashCalculator(), new CatalogScannerOptions { MaxDegreeOfParallelism = 1 });
            var result = await scanner.ScanAsync([folder], CancellationToken.None);
            Assert.Equal(1, result.DiscoveredFiles);
            Assert.Equal(1, result.ProcessedFiles);
            Assert.Single(repository.Files);
            Assert.NotNull(repository.Files[0].Sha256);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private sealed class MemoryRepository : IInstallerFileRepository
    {
        public List<InstallerFile> Files { get; } = [];
        public Task<InstallerFile?> FindByPathAsync(string fullPath, CancellationToken cancellationToken) => Task.FromResult(Files.SingleOrDefault(file => file.FullPath == fullPath));
        public Task UpsertAsync(InstallerFile installerFile, CancellationToken cancellationToken) { Files.RemoveAll(file => file.FullPath == installerFile.FullPath); Files.Add(installerFile); return Task.CompletedTask; }
    }
}
