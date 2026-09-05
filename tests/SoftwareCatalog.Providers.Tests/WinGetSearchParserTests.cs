using SoftwareCatalog.Providers;

namespace SoftwareCatalog.Providers.Tests;

public sealed class WinGetSearchParserTests
{
    [Fact]
    public void ParsesMultiWordNamesWithoutLosingPackageId()
    {
        var rows = WinGetSearchParser.Parse("Mozilla Firefox        Mozilla.Firefox              150.0\nVisual Studio Code     Microsoft.VisualStudioCode   1.99.0\n7-Zip                  7zip.7zip                    26.00");
        Assert.Collection(rows, item => { Assert.Equal("Mozilla Firefox", item.Name); Assert.Equal("Mozilla.Firefox", item.Id); }, item => { Assert.Equal("Visual Studio Code", item.Name); Assert.Equal("Microsoft.VisualStudioCode", item.Id); }, item => Assert.Equal("7zip.7zip", item.Id));
    }
}
