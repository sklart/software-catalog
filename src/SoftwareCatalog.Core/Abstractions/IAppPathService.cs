namespace SoftwareCatalog.Core.Abstractions;

public interface IAppPathService
{
    string ApplicationRoot { get; }
    string DataDirectory { get; }
    string DatabasePath { get; }
    string ConfigDirectory { get; }
    string SettingsPath { get; }
    string LogsDirectory { get; }
    string CacheDirectory { get; }
    string BackupsDirectory { get; }
    void EnsureDirectories();
}
