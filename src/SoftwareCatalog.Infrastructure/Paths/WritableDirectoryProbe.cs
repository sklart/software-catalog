using SoftwareCatalog.Core.Abstractions;

namespace SoftwareCatalog.Infrastructure.Paths;

public sealed class WritableDirectoryProbe : IWritableDirectoryProbe
{
    public bool CanWrite(string directory, out string? error)
    {
        var path = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}.tmp");
        try { Directory.CreateDirectory(directory); using (File.Create(path)) { } File.Delete(path); error = null; return true; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { error = exception.Message; return false; }
    }
}
