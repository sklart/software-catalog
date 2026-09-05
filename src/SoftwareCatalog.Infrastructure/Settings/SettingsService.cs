using System.Text.Json;
using SoftwareCatalog.Core.Abstractions;

namespace SoftwareCatalog.Infrastructure.Settings;

public sealed record AppSettings(int MaxParallelism, string[] SupportedExtensions, int LogRetention, int UpdateCheckCacheHours = 12, int MaxUpdateCheckParallelism = 4)
{
    public static AppSettings Default { get; } = new(Math.Max(1, Environment.ProcessorCount / 2), [".exe", ".msi", ".msix", ".msixbundle", ".zip", ".7z"], 10, 12, 4);
}
public sealed class SettingsService(IAppPathService paths)
{
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.SettingsPath)) { await SaveAsync(AppSettings.Default, cancellationToken); return AppSettings.Default; }
        try { await using var stream = File.OpenRead(paths.SettingsPath); return await JsonSerializer.DeserializeAsync<AppSettings>(stream, cancellationToken: cancellationToken) ?? AppSettings.Default; }
        catch (JsonException) { File.Move(paths.SettingsPath, paths.SettingsPath + ".corrupt", true); await SaveAsync(AppSettings.Default, cancellationToken); return AppSettings.Default; }
    }
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken) { paths.EnsureDirectories(); await File.WriteAllTextAsync(paths.SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }), cancellationToken); }
}
