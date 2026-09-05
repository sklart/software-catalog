using SoftwareCatalog.Core.Abstractions;

namespace SoftwareCatalog.Infrastructure.Logging;

public sealed class PortableLogger(IAppPathService paths, int retention) : IAppLogger
{
    public void Information(string operation, string message) => Write("INFO", operation, message);
    public void Error(string operation, string message) => Write("ERROR", operation, message);
    private void Write(string level, string operation, string message)
    {
        Directory.CreateDirectory(paths.LogsDirectory);
        File.AppendAllText(Path.Combine(paths.LogsDirectory, $"catalog-{DateTime.UtcNow:yyyyMMdd}.log"), $"{DateTimeOffset.UtcNow:O} [{level}] {operation}: {message}{Environment.NewLine}");
        foreach (var file in Directory.EnumerateFiles(paths.LogsDirectory, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).Skip(retention)) File.Delete(file);
    }
}
