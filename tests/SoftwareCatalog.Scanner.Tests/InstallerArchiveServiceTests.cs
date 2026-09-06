using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Scanner;

namespace SoftwareCatalog.Scanner.Tests;

public sealed class InstallerArchiveServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "catalog-archive-" + Guid.NewGuid().ToString("N"));
    public InstallerArchiveServiceTests() => Directory.CreateDirectory(_folder);
    [Fact] public async Task ArchiveRestoreTrashAndPurgePreserveLogicalInstaller()
    {
        var source = Path.Combine(_folder,"source"); Directory.CreateDirectory(source); var path=Path.Combine(source,"tool.exe"); await File.WriteAllTextAsync(path,"content");
        var repo = new Repo(new ScanRoot(1,source,ScanRootPathKind.Absolute,true,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow)); var file=FileRecord(); repo.Files.Add(file);
        var service = Service(repo); var archived=await service.ArchiveAsync(file,CancellationToken.None);
        Assert.Equal(ArchiveOperationStatus.Completed,archived.Status); Assert.False(File.Exists(path)); var afterArchive=Assert.Single(repo.Files); Assert.Equal(file.Id,afterArchive.Id); Assert.Equal(file.ProductId,afterArchive.ProductId); Assert.Equal(InstallerStorageState.Archived,afterArchive.StorageState); Assert.NotEmpty(repo.Operations);
        var restored=await service.RestoreAsync(afterArchive,null,CancellationToken.None); Assert.Equal(ArchiveOperationStatus.Completed,restored.Status); Assert.True(File.Exists(path)); var active=Assert.Single(repo.Files); Assert.Equal(InstallerStorageState.Active,active.StorageState);
        var trashed=await service.TrashAsync(active,CancellationToken.None); Assert.Equal(ArchiveOperationStatus.Completed,trashed.Status); var trash=Assert.Single(repo.Files); Assert.Equal(InstallerStorageState.Trashed,trash.StorageState);
        var purge=await service.PurgeAsync(trash,CancellationToken.None); Assert.Equal(ArchiveOperationStatus.Completed,purge.Status); Assert.False(Assert.Single(repo.Files).Exists); Assert.Contains(repo.Operations,x=>x.OperationType==ArchiveOperationType.Purge && x.Status==ArchiveOperationStatus.Completed);
    }
    [Fact] public async Task PinnedFileIsNeverMovedAndCollisionNeverOverwrites()
    {
        var source=Path.Combine(_folder,"source2"); Directory.CreateDirectory(source); var path=Path.Combine(source,"tool.exe"); await File.WriteAllTextAsync(path,"content"); var repo=new Repo(new ScanRoot(1,source,ScanRootPathKind.Absolute,true,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow)); var pinned=FileRecord() with { IsPinned=true }; repo.Files.Add(pinned);
        var service=Service(repo); var blocked=await service.ArchiveAsync(pinned,CancellationToken.None); Assert.Equal(ArchiveOperationStatus.Error,blocked.Status); Assert.True(File.Exists(path));
        repo.Files[0]=pinned with { IsPinned=false }; var archiveDir=Path.Combine(_folder,"Archive","Tool-11111111","1.0"); Directory.CreateDirectory(archiveDir); var collision=Path.Combine(archiveDir,"tool.exe"); await File.WriteAllTextAsync(collision,"different"); var result=await service.ArchiveAsync(repo.Files[0],CancellationToken.None); Assert.Equal(ArchiveOperationStatus.Conflict,result.Status); Assert.Equal("different",await File.ReadAllTextAsync(collision)); Assert.True(File.Exists(path));
    }
    [Fact] public async Task PinnedArchivedFileCanBeRestoredToAlternateUserRoot()
    {
        var source=Path.Combine(_folder,"source3"); Directory.CreateDirectory(source); var path=Path.Combine(source,"tool.exe"); await File.WriteAllTextAsync(path,"content"); var repo=new Repo(new ScanRoot(1,source,ScanRootPathKind.Absolute,true,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow)); var file=FileRecord(); repo.Files.Add(file); var service=Service(repo); Assert.Equal(ArchiveOperationStatus.Completed,(await service.ArchiveAsync(file,CancellationToken.None)).Status);
        var archived=repo.Files.Single() with { IsPinned=true }; repo.Files[0]=archived; var alternate=Path.Combine(_folder,"alternate","tool.exe"); var restored=await service.RestoreAsync(archived,alternate,CancellationToken.None);
        Assert.Equal(ArchiveOperationStatus.Completed,restored.Status); var active=repo.Files.Single(); Assert.Equal(InstallerStorageState.Active,active.StorageState); Assert.True(active.IsPinned); Assert.Equal(file.Id,active.Id); Assert.Equal(file.ProductId,active.ProductId); Assert.True(File.Exists(alternate)); Assert.Equal(ScanRootRole.User,(await repo.GetScanRootsAsync(CancellationToken.None)).Single(x=>x.Id==active.ScanRootId).Role);
    }
    private InstallerArchiveService Service(Repo repo) => new(repo,new Resolver(),new Locations(Path.Combine(_folder,"Archive")),new FileHashCalculator());
    private static InstallerFile FileRecord() { var now=DateTimeOffset.UtcNow; return new InstallerFile(42,1,"tool.exe","tool.exe",".exe",7,now,null,now,now,true,ProductName:"Tool",ProductVersion:"1.0",NormalizedVersion:"1.0",ProductId:Guid.Parse("11111111-1111-1111-1111-111111111111")); }
    public void Dispose() { if(Directory.Exists(_folder)) Directory.Delete(_folder,true); }
    private sealed class Resolver : IPortablePathResolver { public string Resolve(ScanRoot root)=>root.StoredPath; public string ToStoredPath(string path,ScanRootPathKind kind)=>path; public string GetRelativePath(ScanRoot root,string path)=>Path.GetRelativePath(root.StoredPath,path); public ScanRootAvailability GetAvailability(ScanRoot root)=>ScanRootAvailability.Available; }
    private sealed class Locations(string root) : IArchiveLocationResolver { public string ArchiveRoot=>root; public string TrashRoot=>Path.Combine(root,"Trash"); public string ArchiveStoredPath=>root; public ScanRootPathKind PathKind=>ScanRootPathKind.Absolute; }
    private sealed class Repo(ScanRoot root) : IScanCatalogRepository
    {
        private readonly List<ScanRoot> _roots=[root]; public List<InstallerFile> Files { get; }=[]; public List<ArchiveOperation> Operations { get; }=[];
        public Task<IReadOnlyList<ScanRoot>> GetScanRootsAsync(CancellationToken t)=>Task.FromResult<IReadOnlyList<ScanRoot>>(_roots); public Task<ScanRoot> AddScanRootAsync(string p,ScanRootPathKind k,bool i,CancellationToken t) { var added=new ScanRoot(_roots.Count+1,p,k,i,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);_roots.Add(added);return Task.FromResult(added); } public Task UpdateScanRootAsync(long i,string p,ScanRootPathKind k,CancellationToken t)=>Task.CompletedTask; public Task RemoveScanRootAsync(long i,CancellationToken t)=>Task.CompletedTask; public Task<InstallerFile?> FindInstallerAsync(long r,string p,CancellationToken t)=>Task.FromResult(Files.SingleOrDefault(x=>x.ScanRootId==r&&x.RelativePath==p)); public Task UpsertInstallersAsync(IReadOnlyList<InstallerFile> f,CancellationToken t)=>Task.CompletedTask; public Task MarkMissingAsync(long r,DateTimeOffset s,CancellationToken t)=>Task.CompletedTask; public Task<IReadOnlyList<InstallerFile>> GetInstallersAsync(CancellationToken t)=>Task.FromResult<IReadOnlyList<InstallerFile>>(Files);
        public Task<ScanRoot> EnsureManagedScanRootAsync(ScanRootRole role,string path,ScanRootPathKind kind,CancellationToken t) { var old=_roots.SingleOrDefault(x=>x.Role==role); if(old is not null)return Task.FromResult(old); var next=new ScanRoot(_roots.Count+1,path,kind,true,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,role);_roots.Add(next);return Task.FromResult(next); }
        public Task UpdateInstallerStorageAsync(InstallerStorageUpdate update,CancellationToken t) { var index=Files.FindIndex(x=>x.Id==update.InstallerId); var old=Files[index]; Files[index]=old with { ScanRootId=update.ScanRootId,RelativePath=update.RelativePath,StorageState=update.StorageState,Sha256=update.Sha256??old.Sha256,OriginalScanRootId=update.OriginalScanRootId,OriginalRelativePath=update.OriginalRelativePath,StorageChangedUtc=update.ChangedUtc }; return Task.CompletedTask; }
        public Task SetInstallerPinnedAsync(long id,bool value,CancellationToken t)=>Task.CompletedTask; public Task MarkInstallerPurgedAsync(long id,CancellationToken t) { var i=Files.FindIndex(x=>x.Id==id);Files[i]=Files[i] with { Exists=false };return Task.CompletedTask; } public Task SaveArchiveOperationAsync(ArchiveOperation operation,CancellationToken t) { Operations.RemoveAll(x=>x.Id==operation.Id);Operations.Add(operation);return Task.CompletedTask; } public Task<IReadOnlyList<ArchiveOperation>> GetArchiveOperationsAsync(long? id,CancellationToken t)=>Task.FromResult<IReadOnlyList<ArchiveOperation>>(Operations);
    }
}
