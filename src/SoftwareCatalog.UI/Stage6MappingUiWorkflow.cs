using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI;

public static class Stage6MappingUiWorkflow
{
    public static IReadOnlyList<string> MappingFields { get; } = ["Provider", "ExternalId", "Source", "Confidence", "Enabled", "CreatedUtc", "UpdatedUtc", "LastCheckedUtc"];

    public static async Task<bool> PersistCandidateSelectionAsync(Guid productId, UpdateCandidate? candidate, Func<ProductUpdateSource, Task> save)
    {
        if (candidate is null) return false;
        await save(new ProductUpdateSource(Guid.NewGuid(), productId, candidate.ProviderType, candidate.ExternalId, true, true, MappingSource.Manual, MappingConfidence.Exact));
        return true;
    }
}

public sealed class MappingManagementActions(Func<ProductUpdateSource, Task> save, Func<ProductUpdateSource, Task> remove, Func<Task> search)
{
    public Task EnableAsync(ProductUpdateSource mapping) => save(mapping with { Enabled = true });
    public Task DisableAsync(ProductUpdateSource mapping) => save(mapping with { Enabled = false });
    public Task ChangeExternalIdAsync(ProductUpdateSource mapping, string externalId) => save(mapping with { ExternalId = externalId.Trim() });
    public Task RemoveAsync(ProductUpdateSource mapping) => remove(mapping);
    public Task SearchCandidatesAsync() => search();
}

public sealed class AliasManagementActions(Func<string, Task<ProductAlias>> add, Func<ProductAlias, Task> remove)
{
    public Task<ProductAlias> AddAsync(string alias) => add(alias.Trim());
    public Task RemoveAsync(ProductAlias alias) => remove(alias);
}
