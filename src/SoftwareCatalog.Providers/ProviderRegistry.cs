using SoftwareCatalog.Core.Abstractions;

namespace SoftwareCatalog.Providers;

public sealed class ProviderRegistry(IEnumerable<IUpdateProvider> providers)
{
    public IUpdateProvider? Find(string id) => providers.FirstOrDefault(provider => string.Equals(provider.Id, id, StringComparison.OrdinalIgnoreCase));
}
