using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Scanner;
namespace SoftwareCatalog.Scanner.Tests;
public sealed class CatalogScannerTests
{
    [Fact] public async Task ScansSupportedFileAndReusesUnchangedHash()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder,"setup.exe"),"one");
        try { var repo = new MemoryRepository(); var root = await repo.AddScanRootAsync(folder, ScanRootPathKind.Absolute, true, CancellationToken.None); var hash = new FakeHash(); var scanner = new CatalogScanner(repo, new Resolver(), hash, new CatalogScannerOptions { MaxDegreeOfParallelism = 1 }); await scanner.ScanAsync(root, null, CancellationToken.None); await scanner.ScanAsync(root, null, CancellationToken.None); Assert.Equal(1,hash.Count); Assert.Single(await repo.GetInstallersAsync(CancellationToken.None)); } finally { Directory.Delete(folder,true); }
    }
    [Fact] public async Task ChangedSizeAndModifiedTimeRecalculateHash()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); var path = Path.Combine(folder, "setup.exe"); await File.WriteAllTextAsync(path, "one");
        try { var repo = new MemoryRepository(); var root = await repo.AddScanRootAsync(folder, ScanRootPathKind.Absolute, true, CancellationToken.None); var hash = new FakeHash(); var scanner = new CatalogScanner(repo, new Resolver(), hash, new CatalogScannerOptions { MaxDegreeOfParallelism = 1 }); await scanner.ScanAsync(root, null, CancellationToken.None); await File.WriteAllTextAsync(path, "changed-size"); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2)); await scanner.ScanAsync(root, null, CancellationToken.None); Assert.Equal(2, hash.Count); } finally { Directory.Delete(folder, true); }
    }
    [Fact] public async Task SuccessfulScanMarksDeletedFileMissing()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); var path = Path.Combine(folder, "setup.exe"); await File.WriteAllTextAsync(path, "one");
        try { var repo = new MemoryRepository(); var root = await repo.AddScanRootAsync(folder, ScanRootPathKind.Absolute, true, CancellationToken.None); var scanner = new CatalogScanner(repo, new Resolver(), new FakeHash(), new CatalogScannerOptions { MaxDegreeOfParallelism = 1 }); await scanner.ScanAsync(root, null, CancellationToken.None); File.Delete(path); await scanner.ScanAsync(root, null, CancellationToken.None); Assert.False((await repo.GetInstallersAsync(CancellationToken.None)).Single().Exists); } finally { Directory.Delete(folder, true); }
    }
    private sealed class Resolver : IPortablePathResolver { public string Resolve(ScanRoot r)=>r.StoredPath; public string ToStoredPath(string p,ScanRootPathKind k)=>p; public string GetRelativePath(ScanRoot r,string p)=>Path.GetRelativePath(r.StoredPath,p); public ScanRootAvailability GetAvailability(ScanRoot r)=>ScanRootAvailability.Available; }
    private sealed class FakeHash : IFileHashCalculator { public int Count; public Task<string> ComputeSha256Async(string p,CancellationToken t)=>Task.FromResult((++Count).ToString()); }
    private sealed class MemoryRepository : IScanCatalogRepository
    { private readonly List<ScanRoot> _roots=[]; private readonly List<InstallerFile> _files=[]; public Task<IReadOnlyList<ScanRoot>> GetScanRootsAsync(CancellationToken t)=>Task.FromResult<IReadOnlyList<ScanRoot>>(_roots); public Task<ScanRoot> AddScanRootAsync(string p,ScanRootPathKind k,bool i,CancellationToken t){var r=new ScanRoot(1,p,k,i,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);_roots.Add(r);return Task.FromResult(r);} public Task RemoveScanRootAsync(long i,CancellationToken t)=>Task.CompletedTask; public Task<InstallerFile?> FindInstallerAsync(long r,string p,CancellationToken t)=>Task.FromResult(_files.SingleOrDefault(x=>x.ScanRootId==r&&x.RelativePath==p)); public Task UpsertInstallersAsync(IReadOnlyList<InstallerFile> f,CancellationToken t){foreach(var x in f){_files.RemoveAll(y=>y.ScanRootId==x.ScanRootId&&y.RelativePath==x.RelativePath);_files.Add(x with { Id=_files.Count+1 });}return Task.CompletedTask;} public Task MarkMissingAsync(long r,DateTimeOffset s,CancellationToken t){for(var i=0;i<_files.Count;i++)if(_files[i].ScanRootId==r&&_files[i].LastSeenUtc<s)_files[i]=_files[i] with { Exists=false };return Task.CompletedTask;} public Task<IReadOnlyList<InstallerFile>> GetInstallersAsync(CancellationToken t)=>Task.FromResult<IReadOnlyList<InstallerFile>>(_files); }
}
