using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;

public static class ProductStatusFilter
{
    public static bool Matches(SoftwareProduct product, string? filter) => filter switch
    {
        "Есть обновления" => product.UpdateStatus == UpdateStatus.UpdateAvailable,
        "Актуальные" => product.UpdateStatus == UpdateStatus.UpToDate,
        "Проблемы" => product.UpdateStatus is UpdateStatus.Unknown or UpdateStatus.NotFound or UpdateStatus.Ambiguous or UpdateStatus.Error,
        _ => true
    };
}
