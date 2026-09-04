using SoftwareCatalog.Core.Abstractions;

namespace SoftwareCatalog.Infrastructure.Paths;

public sealed class PortableAppPathService : IAppPathService
{
    public PortableAppPathService() : this(AppContext.BaseDirectory) { }
    public PortableAppPathService(string applicationRoot) => ApplicationRoot = Path.GetFullPath(applicationRoot);
    public string ApplicationRoot { get; }
    public string DataDirectory => Path.Combine(ApplicationRoot, "Data");
    public string DatabasePath => Path.Combine(DataDirectory, "catalog.db");
    public string ConfigDirectory => Path.Combine(ApplicationRoot, "Config");
    public string SettingsPath => Path.Combine(ConfigDirectory, "settings.json");
    public string LogsDirectory => Path.Combine(ApplicationRoot, "Logs");
    public string CacheDirectory => Path.Combine(ApplicationRoot, "Cache");
    public string BackupsDirectory => Path.Combine(ApplicationRoot, "Backups");
    public void EnsureDirectories()
    {
        foreach (var directory in new[] { DataDirectory, ConfigDirectory, LogsDirectory, CacheDirectory, BackupsDirectory }) Directory.CreateDirectory(directory);
    }
}
