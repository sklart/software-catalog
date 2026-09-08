using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI;

public static class ProductSelectionWorkflow
{
    public static IReadOnlyList<SoftwareProduct> Order(IEnumerable<SoftwareProduct> products) => products.OrderBy(product => product.CanonicalName).ToArray();
    public static SoftwareProduct? Confirm(SoftwareProduct? selected) => selected;
    public static SoftwareProduct? Cancel() => null;
}
