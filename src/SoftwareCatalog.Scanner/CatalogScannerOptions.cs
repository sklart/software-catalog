namespace SoftwareCatalog.Scanner;

public sealed class CatalogScannerOptions
{
    public int MaxDegreeOfParallelism { get; init; } = Math.Max(1, Environment.ProcessorCount / 2);
    public ISet<string> SupportedExtensions { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".exe", ".msi", ".msix", ".msixbundle", ".zip", ".7z" };
}
