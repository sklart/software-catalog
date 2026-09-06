using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Core.Abstractions;

namespace SoftwareCatalog.Core;

public enum RetentionAction { Keep, Archive, DuplicateCandidate, ManualReview }
public sealed class RetentionPlanItem(InstallerFile installer, RetentionAction action, string reason, bool selected = true)
{
    public InstallerFile Installer { get; } = installer;
    public RetentionAction Action { get; } = action;
    public string Reason { get; } = reason;
    public bool Selected { get; set; } = selected;
}
public sealed record RetentionPlan(IReadOnlyList<RetentionPlanItem> Items, int KeepLatestVersions)
{
    public long EstimatedActiveSpaceFreed => Items.Where(x => x.Action == RetentionAction.Archive).Sum(x => x.Installer.Size);
}
public sealed record CatalogSpaceStatistics(long TotalBytes, long ActiveBytes, long ArchivedBytes, long TrashBytes, long DuplicateBytesPotentiallyRecoverable);

public sealed class DuplicateInstallerService
{
    public IReadOnlyDictionary<long, RetentionAction> Classify(IEnumerable<InstallerFile> files)
    {
        var output = new Dictionary<long, RetentionAction>();
        foreach (var group in files.Where(x => x.Exists && !string.IsNullOrWhiteSpace(x.Sha256)).GroupBy(x => x.Sha256!, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
        {
            var productIds = group.Select(x => x.ProductId).Distinct().ToList();
            if (productIds.Count > 1 || productIds.Any(x => x is null)) foreach (var item in group) output[item.Id] = RetentionAction.ManualReview;
            else
            {
                var canonical = group.OrderBy(Priority).ThenBy(x => x.Id).First();
                foreach (var item in group.Where(x => x.Id != canonical.Id)) output[item.Id] = RetentionAction.DuplicateCandidate;
            }
        }
        return output;
    }
    private static int Priority(InstallerFile file) => file.IsPinned ? 0 : file.StorageState switch { InstallerStorageState.Active => 1, InstallerStorageState.Archived => 2, InstallerStorageState.Trashed => 3, _ => 4 };
}

public sealed class InstallerRetentionPlanner(VersionComparer comparer, DuplicateInstallerService duplicates, IAppLogger? logger = null)
{
    public RetentionPlan CreatePlan(IEnumerable<InstallerFile> installers, int keepLatestVersions)
    {
        var keep = Math.Max(1, keepLatestVersions); var files = installers.Where(x => x.Exists && x.StorageState != InstallerStorageState.Trashed).ToList();
        var result = new Dictionary<long, RetentionPlanItem>();
        foreach (var group in files.Where(x => x.ProductId is not null).GroupBy(x => x.ProductId!.Value))
        {
            var known = group.Where(x => !string.IsNullOrWhiteSpace(x.NormalizedVersion) && comparer.TryParse(x.NormalizedVersion, out _)).ToList();
            foreach (var unknown in group.Except(known)) result[unknown.Id] = new(unknown, unknown.IsPinned ? RetentionAction.Keep : RetentionAction.ManualReview, unknown.IsPinned ? "Закреплён" : "Неизвестная версия");
            var versions = known.GroupBy(x => x.NormalizedVersion!, StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Key, new NormalizedVersionComparer(comparer)).ToList();
            foreach (var version in versions)
            {
                var retained = versions.IndexOf(version) < keep;
                foreach (var file in version)
                {
                    var action = file.IsPinned || retained || file.StorageState == InstallerStorageState.Archived ? RetentionAction.Keep : RetentionAction.Archive;
                    result[file.Id] = new(file, action, file.IsPinned ? "Закреплён" : retained ? "Одна из последних версий" : file.StorageState == InstallerStorageState.Archived ? "Уже в архиве" : "Старая версия");
                }
            }
        }
        foreach (var file in files.Where(x => x.ProductId is null)) result[file.Id] = new(file, RetentionAction.ManualReview, "Продукт не определён");
        foreach (var pair in duplicates.Classify(files)) if (result.TryGetValue(pair.Key, out var item) && !item.Installer.IsPinned) result[pair.Key] = new RetentionPlanItem(item.Installer, pair.Value, pair.Value == RetentionAction.DuplicateCandidate ? "Дубликат SHA-256" : "Дубликат между продуктами", item.Selected);
        var plan = new RetentionPlan(result.Values.OrderBy(x => x.Installer.ProductName).ThenBy(x => x.Installer.Id).ToList(), keep);
        logger?.Information("retention", $"keep={plan.Items.Count(x => x.Action == RetentionAction.Keep)} archive={plan.Items.Count(x => x.Action == RetentionAction.Archive)} duplicates={plan.Items.Count(x => x.Action == RetentionAction.DuplicateCandidate)} manualReview={plan.Items.Count(x => x.Action == RetentionAction.ManualReview)}");
        return plan;
    }
    public CatalogSpaceStatistics GetSpaceStatistics(IEnumerable<InstallerFile> files)
    {
        var list = files.Where(x => x.Exists).ToList(); var duplicate = duplicates.Classify(list).Where(x => x.Value == RetentionAction.DuplicateCandidate).Select(x => list.Single(f => f.Id == x.Key).Size).Sum();
        return new(list.Sum(x => x.Size), list.Where(x => x.StorageState == InstallerStorageState.Active).Sum(x => x.Size), list.Where(x => x.StorageState == InstallerStorageState.Archived).Sum(x => x.Size), list.Where(x => x.StorageState == InstallerStorageState.Trashed).Sum(x => x.Size), duplicate);
    }
    private sealed class NormalizedVersionComparer(VersionComparer comparer) : IComparer<string> { public int Compare(string? x, string? y) => comparer.Compare(x, y) switch { VersionComparisonResult.Older => -1, VersionComparisonResult.Newer => 1, _ => string.CompareOrdinal(x, y) }; }
}
