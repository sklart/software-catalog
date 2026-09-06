using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Tests;

public sealed class InstallerRetentionPlannerTests
{
    private readonly InstallerRetentionPlanner _planner = new(new VersionComparer(), new DuplicateInstallerService());
    [Fact] public void KeepsThreeNewestVersionsAndArchivesOlder()
    {
        var plan = _planner.CreatePlan([File(1,"1.0"), File(2,"2.0"), File(3,"3.0"), File(4,"4.0")], 3);
        Assert.Equal(RetentionAction.Archive, Item(plan, 1).Action);
        Assert.All([2L,3,4], id => Assert.Equal(RetentionAction.Keep, Item(plan, id).Action));
    }
    [Fact] public void OrdersNumericVersionsInsteadOfLexicalVersions()
    {
        var plan = _planner.CreatePlan([File(1,"1.9"), File(2,"1.10"), File(3,"2.0")], 2);
        Assert.Equal(RetentionAction.Archive, Item(plan, 1).Action);
        Assert.Equal(RetentionAction.Keep, Item(plan, 2).Action);
    }
    [Fact] public void KeepsAllArchitecturesOfRetainedVersion()
    {
        var plan = _planner.CreatePlan([File(1,"2.0"), File(2,"3.0", architecture:"x86"), File(3,"3.0",architecture:"x64"), File(4,"3.0",architecture:"arm64")], 1);
        Assert.Equal(RetentionAction.Archive, Item(plan, 1).Action); Assert.All([2L,3,4], id => Assert.Equal(RetentionAction.Keep, Item(plan,id).Action));
    }
    [Fact] public void ProtectsPinnedAndUnknownInstallers()
    {
        var plan = _planner.CreatePlan([File(1,"1.0", pinned:true), File(2,"2.0"), File(3,null)], 1);
        Assert.Equal(RetentionAction.Keep, Item(plan,1).Action); Assert.Equal(RetentionAction.ManualReview,Item(plan,3).Action);
    }
    [Fact] public void DoesNotReArchiveArchivedInstaller()
    {
        var plan = _planner.CreatePlan([File(1,"1.0", state:InstallerStorageState.Archived), File(2,"2.0")], 1);
        Assert.Equal(RetentionAction.Keep, Item(plan,1).Action);
    }
    [Fact] public void NormalizesZeroAndExcludesTrashAndNullHashes()
    {
        var plan = _planner.CreatePlan([File(1,"1.0"), File(2,"2.0"), File(3,"9.0",state:InstallerStorageState.Trashed)], 0);
        Assert.Equal(1,plan.KeepLatestVersions); Assert.DoesNotContain(plan.Items,x=>x.Installer.Id==3); Assert.Equal(RetentionAction.Archive,Item(plan,1).Action);
        Assert.Empty(new DuplicateInstallerService().Classify([File(4,"1.0"),File(5,"1.0")]));
    }
    [Fact] public void CanonicalDuplicatePrefersPinnedThenActiveThenArchived()
    {
        var service=new DuplicateInstallerService(); var copies=new[] { File(1,"1.0",sha:"A",state:InstallerStorageState.Trashed), File(2,"1.0",sha:"A",state:InstallerStorageState.Archived), File(3,"1.0",sha:"A"), File(4,"1.0",sha:"A",pinned:true,state:InstallerStorageState.Trashed) };
        var classifications=service.Classify(copies); Assert.DoesNotContain(4L,classifications.Keys); Assert.Equal(RetentionAction.DuplicateCandidate,classifications[1]); Assert.Equal(RetentionAction.DuplicateCandidate,classifications[2]); Assert.Equal(RetentionAction.DuplicateCandidate,classifications[3]);
    }
    [Fact] public void ComputesKnownSpaceStatistics()
    {
        var stats=_planner.GetSpaceStatistics([File(1,"1.0",sha:"A") with { Size=10 },File(2,"1.0",sha:"A") with { Size=10 },File(3,"1.0",state:InstallerStorageState.Archived) with { Size=20 },File(4,"1.0",state:InstallerStorageState.Trashed) with { Size=30 }]);
        Assert.Equal(70,stats.TotalBytes); Assert.Equal(20,stats.ActiveBytes); Assert.Equal(20,stats.ArchivedBytes); Assert.Equal(30,stats.TrashBytes); Assert.Equal(10,stats.DuplicateBytesPotentiallyRecoverable);
    }
    [Fact] public void ClassifiesDuplicatesWithoutRiskingLastCopy()
    {
        var sameProduct = _planner.CreatePlan([File(1,"1.0",sha:"A"), File(2,"1.0",sha:"A")], 1);
        Assert.Equal(1, sameProduct.Items.Count(x => x.Action == RetentionAction.DuplicateCandidate));
        var crossProduct = _planner.CreatePlan([File(1,"1.0",sha:"A", product:Guid.NewGuid()), File(2,"1.0",sha:"A", product:Guid.NewGuid())], 1);
        Assert.All(crossProduct.Items, x => Assert.Equal(RetentionAction.ManualReview,x.Action));
    }
    [Fact] public void NullProductInSameShaGroupRequiresManualReview()
    {
        var unknown = File(1, "1.0", sha:"A") with { ProductId = null };
        var known = File(2, "1.0", sha:"A");
        var classifications = new DuplicateInstallerService().Classify([unknown, known]);
        Assert.All(classifications.Values, action => Assert.Equal(RetentionAction.ManualReview, action));
    }
    [Fact] public void LogsSinglePlanSummary()
    {
        var logger = new TestLogger(); var planner = new InstallerRetentionPlanner(new VersionComparer(), new DuplicateInstallerService(), logger);
        planner.CreatePlan([File(1,"1.0"), File(2,"2.0"), File(3,null), File(4,"1.0",sha:"A"), File(5,"1.0",sha:"A")], 1);
        var message = Assert.Single(logger.Messages); Assert.Contains("keep=", message); Assert.Contains("archive=", message); Assert.Contains("duplicates=", message); Assert.Contains("manualReview=", message);
    }
    private static RetentionPlanItem Item(RetentionPlan plan, long id) => plan.Items.Single(x => x.Installer.Id == id);
    private static InstallerFile File(long id, string? version, bool pinned=false, InstallerStorageState state=InstallerStorageState.Active, string? sha=null, Guid? product=null, string? architecture=null)
    { var now=DateTimeOffset.UtcNow; return new InstallerFile(id,1,$"{id}.exe",$"{id}.exe",".exe",100,now,sha,now,now,true,ProductName:"Tool",ProductVersion:version,NormalizedVersion:version,ProductId:product ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),Architecture:architecture,StorageState:state,IsPinned:pinned); }
    private sealed class TestLogger : SoftwareCatalog.Core.Abstractions.IAppLogger { public List<string> Messages { get; }=[]; public void Information(string operation,string message)=>Messages.Add(message); public void Error(string operation,string message)=>throw new InvalidOperationException(message); }
}
