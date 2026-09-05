using SoftwareCatalog.Core.Abstractions;

namespace SoftwareCatalog.Core.Tests;
public sealed class UpdateProviderContractTests
{
    [Fact]
    public void ProviderContractContainsOnlyProductSourceAndCancellationContext()
    {
        var method = typeof(IUpdateProvider).GetMethod(nameof(IUpdateProvider.CheckLatestAsync))!; var types = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
        Assert.DoesNotContain(types, type => type.Name.Contains("InstallerFile", StringComparison.Ordinal) || type.Name.Contains("Path", StringComparison.Ordinal) || type.Name.Contains("Hash", StringComparison.Ordinal));
        Assert.Equal(3, types.Length);
    }
}
