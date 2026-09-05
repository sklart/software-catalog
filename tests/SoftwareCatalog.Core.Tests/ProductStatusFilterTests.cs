using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Tests;
public sealed class ProductStatusFilterTests
{
    [Fact] public void AppliesDocumentedPolicy() { var now = DateTimeOffset.UtcNow; var products = Enum.GetValues<UpdateStatus>().Select(status => new SoftwareProduct(Guid.NewGuid(), status.ToString(), null, status.ToString(), now, now, UpdateStatus: status)).ToArray(); Assert.Single(products, p => ProductStatusFilter.Matches(p, "Есть обновления")); Assert.Single(products, p => ProductStatusFilter.Matches(p, "Актуальные")); Assert.Equal(4, products.Count(p => ProductStatusFilter.Matches(p, "Проблемы"))); Assert.Equal(products.Length, products.Count(p => ProductStatusFilter.Matches(p, "Все"))); }
}
