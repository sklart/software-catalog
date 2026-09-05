using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Scanner;

namespace SoftwareCatalog.Scanner.Tests;

public sealed class MetadataScannerIntegrationTests
{
    [Fact]
    public async Task ReusesSuccessfulMetadataForUnchangedFileAndRefreshesChangedFile()
    {
        var folder = CreateFolder(); var path = Path.Combine(folder, "Tool-1.0-x86.exe"); await File.WriteAllTextAsync(path, "one");
        try
        {
            var extractor = new CountingExtractor(); var repo = new Repository(); var root = await repo.AddScanRootAsync(folder, ScanRootPathKind.Absolute, true, default);
            var scanner = CreateScanner(repo, extractor); await scanner.ScanAsync(root, null, default); await scanner.ScanAsync(root, null, default);
            Assert.Equal(1, extractor.Calls); Assert.Equal("Native 1", (await repo.GetInstallersAsync(default)).Single().ProductName);
            await File.WriteAllTextAsync(path, "changed"); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2)); await scanner.ScanAsync(root, null, default);
            Assert.Equal(2, extractor.Calls); Assert.Equal("Native 2", (await repo.GetInstallersAsync(default)).Single().ProductName);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task FailedMetadataPersistsAndIsRetriedWithoutAbortingScan()
    {
        var folder = CreateFolder(); await File.WriteAllTextAsync(Path.Combine(folder, "bad.exe"), "bad");
        try
        {
            var extractor = new CountingExtractor { FailFirst = true }; var repo = new Repository(); var root = await repo.AddScanRootAsync(folder, ScanRootPathKind.Absolute, true, default);
            var scanner = CreateScanner(repo, extractor); var first = await scanner.ScanAsync(root, null, default);
            var file = (await repo.GetInstallersAsync(default)).Single(); Assert.True(first.Completed); Assert.Equal(MetadataStatus.Failed, file.MetadataStatus); Assert.Equal("synthetic metadata failure", file.MetadataError);
            await scanner.ScanAsync(root, null, default); file = (await repo.GetInstallersAsync(default)).Single(); Assert.Equal(2, extractor.Calls); Assert.Equal(MetadataStatus.Success, file.MetadataStatus);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task MetadataFailureDoesNotStopOtherFilesAndIsLogged()
    {
        var folder = CreateFolder(); await File.WriteAllTextAsync(Path.Combine(folder, "good-one.exe"), "1"); await File.WriteAllTextAsync(Path.Combine(folder, "bad.exe"), "2"); await File.WriteAllTextAsync(Path.Combine(folder, "good-two.exe"), "3");
        try
        {
            var extractor = new CountingExtractor { FailBadPath = true }; var logger = new Logger(); var repo = new Repository(); var root = await repo.AddScanRootAsync(folder, ScanRootPathKind.Absolute, true, default);
            var result = await CreateScanner(repo, extractor, logger).ScanAsync(root, null, default);
            var files = await repo.GetInstallersAsync(default); Assert.True(result.Completed); Assert.Equal(3, result.ProcessedFiles); Assert.Equal(3, files.Count); Assert.Equal(MetadataStatus.Failed, files.Single(file => file.FileName == "bad.exe").MetadataStatus);
            Assert.Contains(logger.Errors, entry => entry.Contains("metadata") && entry.Contains("bad.exe") && entry.Contains("Executable") && entry.Contains("synthetic metadata failure"));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task EmbeddedMetadataWinsOverFilenameFallback()
    {
        var folder = CreateFolder(); await File.WriteAllTextAsync(Path.Combine(folder, "WrongProduct-9.9-x86.exe"), "x");
        try
        {
            var extractor = new CountingExtractor { NativeName = "Correct Product", NativeVersion = "1.2.3", NativeArchitecture = "x64" }; var repo = new Repository(); var root = await repo.AddScanRootAsync(folder, ScanRootPathKind.Absolute, true, default);
            await CreateScanner(repo, extractor).ScanAsync(root, null, default); var file = (await repo.GetInstallersAsync(default)).Single();
            Assert.Equal("Correct Product", file.ProductName); Assert.Equal("1.2.3", file.ProductVersion); Assert.Equal("x64", file.Architecture);
        }
        finally { Directory.Delete(folder, true); }
    }

    private static CatalogScanner CreateScanner(Repository repo, CountingExtractor extractor, IAppLogger? logger = null) => new(repo, new Resolver(), new Hash(), new CatalogScannerOptions { MaxDegreeOfParallelism = 1 }, logger, metadataService: new InstallerMetadataService([extractor]));
    private static string CreateFolder() { var value = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(value); return value; }
    private sealed class CountingExtractor : IInstallerMetadataExtractor
    {
        public int Calls; public bool FailFirst; public bool FailBadPath; public string NativeName = "Native 1"; public string NativeVersion = "1.0.0"; public string NativeArchitecture = "x64";
        public bool CanExtract(InstallerKind kind) => kind == InstallerKind.Executable;
        public InstallerMetadata Extract(string path, InstallerKind kind) { Calls++; if ((FailFirst && Calls == 1) || (FailBadPath && Path.GetFileName(path) == "bad.exe")) return new(kind, Source: MetadataSource.PeVersionInfo, Status: MetadataStatus.Failed, Error: "synthetic metadata failure"); return new(kind, NativeName == "Native 1" ? $"Native {Calls}" : NativeName, NativeVersion, "Publisher", Architecture: NativeArchitecture, Source: MetadataSource.PeVersionInfo, Status: MetadataStatus.Success); }
    }
    private sealed class Hash : IFileHashCalculator { public Task<string> ComputeSha256Async(string path, CancellationToken token) => Task.FromResult("hash"); }
    private sealed class Logger : IAppLogger { public List<string> Errors { get; } = []; public void Information(string operation, string message) { } public void Error(string operation, string message) => Errors.Add($"{operation} {message}"); }
    private sealed class Resolver : IPortablePathResolver { public string Resolve(ScanRoot root) => root.StoredPath; public string ToStoredPath(string path, ScanRootPathKind kind) => path; public string GetRelativePath(ScanRoot root, string path) => Path.GetRelativePath(root.StoredPath, path); public ScanRootAvailability GetAvailability(ScanRoot root) => ScanRootAvailability.Available; }
    private sealed class Repository : IScanCatalogRepository
    {
        private readonly List<InstallerFile> _files = []; private ScanRoot? _root;
        public Task<IReadOnlyList<ScanRoot>> GetScanRootsAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<ScanRoot>>(_root is null ? [] : [_root]);
        public Task<ScanRoot> AddScanRootAsync(string path, ScanRootPathKind kind, bool include, CancellationToken token) { _root = new ScanRoot(1, path, kind, include, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow); return Task.FromResult(_root); }
        public Task UpdateScanRootAsync(long id, string path, ScanRootPathKind kind, CancellationToken token) => Task.CompletedTask; public Task RemoveScanRootAsync(long id, CancellationToken token) => Task.CompletedTask;
        public Task<InstallerFile?> FindInstallerAsync(long rootId, string relative, CancellationToken token) => Task.FromResult(_files.SingleOrDefault(file => file.ScanRootId == rootId && file.RelativePath == relative));
        public Task UpsertInstallersAsync(IReadOnlyList<InstallerFile> files, CancellationToken token) { foreach (var file in files) { _files.RemoveAll(value => value.ScanRootId == file.ScanRootId && value.RelativePath == file.RelativePath); _files.Add(file with { Id = file.Id == 0 ? _files.Count + 1 : file.Id }); } return Task.CompletedTask; }
        public Task MarkMissingAsync(long rootId, DateTimeOffset started, CancellationToken token) { for (var i = 0; i < _files.Count; i++) if (_files[i].ScanRootId == rootId && _files[i].LastSeenUtc < started) _files[i] = _files[i] with { Exists = false }; return Task.CompletedTask; }
        public Task<IReadOnlyList<InstallerFile>> GetInstallersAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<InstallerFile>>(_files);
    }
}
