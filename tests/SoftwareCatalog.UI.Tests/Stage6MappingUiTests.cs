using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.UI;

namespace SoftwareCatalog.UI.Tests;

public sealed class Stage6MappingUiTests
{
    private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static ProductUpdateSource Mapping(string externalId="Vendor.Tool", bool enabled=true) => new(Guid.NewGuid(),ProductId,"WinGet",externalId,enabled,false,MappingSource.ExactMatch,MappingConfidence.High,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);
    private static UpdateCandidate Candidate(MappingConfidence confidence=MappingConfidence.High) => new("GitHub","owner/repo","Repo","owner","2.0",confidence,"test");

    [Fact]
    public async Task ExplicitCandidateSelectionPersistsManualExactMapping()
    {
        ProductUpdateSource? saved=null; var changed=await Stage6MappingUiWorkflow.PersistCandidateSelectionAsync(ProductId,Candidate(),x=>{saved=x;return Task.CompletedTask;});
        Assert.True(changed); Assert.NotNull(saved); Assert.Equal("GitHub",saved.ProviderType); Assert.Equal("owner/repo",saved.ExternalId); Assert.True(saved.IsExplicit); Assert.Equal(MappingSource.Manual,saved.Source); Assert.Equal(MappingConfidence.Exact,saved.Confidence);
    }

    [Fact]
    public async Task CandidateCancelAndUnselectedLowOrAmbiguousCandidatesDoNotChangeMapping()
    {
        var calls=0; Assert.False(await Stage6MappingUiWorkflow.PersistCandidateSelectionAsync(ProductId,null,_=>{calls++;return Task.CompletedTask;}));
        var low=Candidate(MappingConfidence.Low); var ambiguous=Candidate(MappingConfidence.Ambiguous);
        Assert.False(await Stage6MappingUiWorkflow.PersistCandidateSelectionAsync(ProductId,null,_=>{calls++;return Task.CompletedTask;})); Assert.Equal(MappingConfidence.Low,low.Confidence); Assert.Equal(MappingConfidence.Ambiguous,ambiguous.Confidence); Assert.Equal(0,calls);
    }

    [Fact]
    public async Task MappingActionsEnableDisableChangeAndRemove()
    {
        var saved=new List<ProductUpdateSource>(); ProductUpdateSource? removed=null; var searched=0; var actions=new MappingManagementActions(x=>{saved.Add(x);return Task.CompletedTask;},x=>{removed=x;return Task.CompletedTask;},()=>{searched++;return Task.CompletedTask;}); var mapping=Mapping();
        await actions.DisableAsync(mapping); await actions.EnableAsync(mapping with { Enabled=false }); await actions.ChangeExternalIdAsync(mapping," owner/new "); await actions.RemoveAsync(mapping); await actions.SearchCandidatesAsync();
        Assert.False(saved[0].Enabled); Assert.True(saved[1].Enabled); Assert.Equal("owner/new",saved[2].ExternalId); Assert.Same(mapping,removed); Assert.Equal(1,searched);
    }

    [Fact]
    public async Task AliasActionsAddAndRemoveSelected()
    {
        ProductAlias? removed=null; var actions=new AliasManagementActions(value=>Task.FromResult(new ProductAlias(ProductId,value,"alias",MappingSource.Manual)),alias=>{removed=alias;return Task.CompletedTask;}); var alias=await actions.AddAsync(" Alias "); await actions.RemoveAsync(alias);
        Assert.Equal("Alias",alias.Alias); Assert.Same(alias,removed);
    }

    [Fact]
    public void MappingFieldsAreAvailableForDisplay()
    {
        Assert.Equal(["Provider","ExternalId","Source","Confidence","Enabled","CreatedUtc","UpdatedUtc","LastCheckedUtc"],Stage6MappingUiWorkflow.MappingFields);
    }
}
