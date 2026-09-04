using SoftwareCatalog.Core.Abstractions;

namespace SoftwareCatalog.Infrastructure.Logging;

public sealed class PortableLogger(IAppPathService paths, int retention)
{
    public void Information(string message) => Write("INFO", message);
    public void Error(string message) => Write("ERROR", message);
    private void Write(string level, string message)
    {
        Directory.CreateDirectory(paths.LogsDirectory);
        File.AppendAllText(Path.Combine(paths.LogsDirectory, $"catalog-{DateTime.UtcNow:yyyyMMdd}.log"), $"{DateTimeOffset.UtcNow:O} [{level}] {message}{Environment.NewLine}");
        foreach (var file in Directory.EnumerateFiles(paths.LogsDirectory, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).Skip(retention)) File.Delete(file);
    }
}
