namespace SoftwareCatalog.Database;

public static class CatalogPaths
{
    public static string ApplicationDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoftwareCatalog");
    public static string DatabasePath => Path.Combine(ApplicationDirectory, "catalog.db");
    public static string LogsDirectory => Path.Combine(ApplicationDirectory, "Logs");
}
