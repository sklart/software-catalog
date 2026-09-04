using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Providers;

namespace SoftwareCatalog.Providers.Tests;

public sealed class ProviderRegistryTests
{
    [Fact]
    public void Find_IsCaseInsensitive() => Assert.NotNull(new ProviderRegistry([new FakeProvider()]).Find("test"));

    private sealed class FakeProvider : IUpdateProvider
    {
        public string Id => "Test";
        public Task<UpdateResult> CheckAsync(SoftwareProduct product, CancellationToken cancellationToken) => Task.FromResult(new UpdateResult(null, "Unknown"));
    }
}
