using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;

public interface IUpdateChecker { Task<UpdateCheckResult> CheckAsync(SoftwareProduct product, bool force, int cacheHours, CancellationToken cancellationToken); }

public sealed class UpdateDiscoveryService(IProductCatalogRepository repository, IEnumerable<IUpdateProvider> providers, VersionComparer comparer, IAppLogger? logger = null) : IUpdateChecker
{
    private static readonly string[] Priority = ["WinGet", "GitHub", "GitHubTags"];
    public async Task<UpdateCheckResult> CheckAsync(SoftwareProduct product, bool force, int cacheHours, CancellationToken cancellationToken)
    {
        if (!force && product.LastCheckedUtc is { } checkedUtc && checkedUtc > DateTimeOffset.UtcNow.AddHours(-cacheHours)) return new(product.UpdateStatus, product.LatestVersion, product.LatestNormalizedVersion, Source: product.UpdateProvider, ExternalProductId: product.ExternalProductId, Error: product.UpdateError, CheckedUtc: checkedUtc);
        var sources = await repository.GetUpdateSourcesAsync(product.Id, cancellationToken);
        var orderedSources = sources.Where(s => s.Enabled && (s.IsExplicit || s.Confidence is MappingConfidence.Exact or MappingConfidence.High)).OrderByDescending(s => s.IsExplicit).ThenBy(s => Rank(s.ProviderType)).ToList();
        UpdateCheckResult? lastFailure = null;
        foreach (var source in orderedSources)
        {
            var outcome = await TryProvider(product, source, cancellationToken);
            if (outcome?.Status == UpdateStatus.Error) lastFailure = outcome;
            if (outcome is { Status: not UpdateStatus.NotFound and not UpdateStatus.Error }) return await PersistCompared(product, outcome, cancellationToken);
            if (source.ProviderType.Equals("GitHub", StringComparison.OrdinalIgnoreCase) && outcome?.Status == UpdateStatus.NotFound)
            {
                var tags = providers.FirstOrDefault(x => x.Id.Equals("GitHubTags", StringComparison.OrdinalIgnoreCase));
                if (tags is not null) { var tagResult=await TryProvider(product, source with { ProviderType="GitHubTags" }, cancellationToken); if(tagResult is { Status: not UpdateStatus.NotFound and not UpdateStatus.Error }) return await PersistCompared(product,tagResult,cancellationToken); }
            }
        }
        foreach (var provider in providers.OrderBy(x=>Rank(x.Id)))
        {
            if (!provider.CanHandle(product, null)) continue;
            var outcome=await TryProvider(product,null,cancellationToken,provider);
            if (outcome?.Status == UpdateStatus.Error) lastFailure = outcome;
            if(outcome is { Status: not UpdateStatus.NotFound and not UpdateStatus.Error })
            {
                if(outcome.Status==UpdateStatus.Unknown && !string.IsNullOrWhiteSpace(outcome.ExternalProductId)) await repository.SetUpdateSourceAsync(new ProductUpdateSource(Guid.NewGuid(),product.Id,provider.Id,outcome.ExternalProductId,true,false,MappingSource.ExactMatch,MappingConfidence.Exact),cancellationToken);
                return await PersistCompared(product,outcome,cancellationToken);
            }
        }
        var aliases=await repository.GetProductAliasesAsync(product.Id,cancellationToken);
        var githubCandidates=(await Task.WhenAll(providers.OfType<IUpdateCandidateProvider>().Select(async candidateProvider => { try { return await candidateProvider.SearchCandidatesAsync(product,aliases,cancellationToken); } catch(OperationCanceledException) { throw; } catch { return (IReadOnlyList<UpdateCandidate>)[]; } }))).SelectMany(x=>x).Where(x=>x.ProviderType.Equals("GitHub",StringComparison.OrdinalIgnoreCase) && x.Confidence is MappingConfidence.Exact or MappingConfidence.High).GroupBy(x=>x.ExternalId,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToArray();
        if(githubCandidates.Length==1)
        {
            var source=new ProductUpdateSource(Guid.NewGuid(),product.Id,"GitHub",githubCandidates[0].ExternalId,true,false,MappingSource.ProviderSearch,githubCandidates[0].Confidence);
            var outcome=await TryProvider(product,source,cancellationToken);
            if(outcome?.Status==UpdateStatus.NotFound) { var tags=providers.FirstOrDefault(x=>x.Id.Equals("GitHubTags",StringComparison.OrdinalIgnoreCase)); if(tags is not null) outcome=await TryProvider(product,source with { ProviderType="GitHubTags" },cancellationToken,tags); }
            if(outcome is { Status: not UpdateStatus.NotFound and not UpdateStatus.Error }) { await repository.SetUpdateSourceAsync(source,cancellationToken); return await PersistCompared(product,outcome,cancellationToken); }
            if(outcome?.Status==UpdateStatus.Error) lastFailure=outcome;
        }
        return await Persist(product.Id,lastFailure ?? new(UpdateStatus.NotFound,Error:"No reliable update source configured",CheckedUtc:DateTimeOffset.UtcNow,ErrorKind:ProviderErrorKind.NotFound),cancellationToken);
    }
    private async Task<UpdateCheckResult> Persist(Guid id, UpdateCheckResult result, CancellationToken token) { await repository.SaveUpdateCheckAsync(id, result, token); return result; }
    private static int Rank(string id) { var rank=Array.FindIndex(Priority,x=>x.Equals(id,StringComparison.OrdinalIgnoreCase)); return rank<0?int.MaxValue:rank; }
    private async Task<UpdateCheckResult?> TryProvider(SoftwareProduct product, ProductUpdateSource? source, CancellationToken token, IUpdateProvider? requestedProvider = null) { var provider=requestedProvider ?? (source is null ? providers.OrderBy(x=>Rank(x.Id)).FirstOrDefault(x=>x.CanHandle(product,null)) : providers.FirstOrDefault(x=>x.CanHandle(product,source))); if(provider is null)return null; try { var result=await provider.CheckLatestAsync(product,source,token); return result with { Source=result.Source??provider.Id }; } catch(OperationCanceledException){throw;}catch(Exception ex){logger?.Error("update",$"Provider {provider.Id} failed for product {product.Id}: {ex.Message}");return new(UpdateStatus.Error,Source:provider.Id,ExternalProductId:source?.ExternalId,Error:ex.Message,ErrorKind:ProviderErrorKind.NetworkError);}}
    private async Task<UpdateCheckResult> PersistCompared(SoftwareProduct product, UpdateCheckResult result, CancellationToken token) { if(result.Status==UpdateStatus.Unknown && result.LatestNormalizedVersion is not null) result=result with { Status=comparer.Compare(product.LatestNormalizedVersion??product.LatestLocalVersion,result.LatestNormalizedVersion) switch { VersionComparisonResult.Older=>UpdateStatus.UpdateAvailable,VersionComparisonResult.Equal=>UpdateStatus.UpToDate,VersionComparisonResult.Newer=>UpdateStatus.LocalNewer,_=>UpdateStatus.Unknown } }; return await Persist(product.Id,result with { CheckedUtc=DateTimeOffset.UtcNow },token); }
}

public sealed class UpdateCandidateDiscoveryService(IProductCatalogRepository repository, IEnumerable<IUpdateProvider> providers)
{
    public async Task<IReadOnlyList<UpdateCandidate>> SearchAsync(SoftwareProduct product, bool force, int cacheHours, CancellationToken token)
    {
        if (!force && await repository.IsUpdateCandidateCacheFreshAsync(product.Id, cacheHours, token)) return await repository.SearchUpdateCandidatesAsync(product.Id, false, cacheHours, token);
        var aliases = await repository.GetProductAliasesAsync(product.Id, token);
        var work = providers.OfType<IUpdateCandidateProvider>().Select(async provider => { try { return await provider.SearchCandidatesAsync(product, aliases, token); } catch (OperationCanceledException) { throw; } catch { return (IReadOnlyList<UpdateCandidate>)[]; } });
        var candidates = (await Task.WhenAll(work)).SelectMany(x => x).OrderBy(x => x.Confidence).ThenBy(x => x.ProviderType, StringComparer.Ordinal).ThenBy(x => x.ExternalId, StringComparer.Ordinal).ToArray();
        await repository.SaveUpdateCandidatesAsync(product.Id, candidates, DateTimeOffset.UtcNow, token);
        return candidates;
    }
}
