namespace SoftwareCatalog.Scanner;

public interface IArchiveFileSystem
{
    bool Exists(string path);
    long GetLength(string path);
    void CreateDirectory(string path);
    Task CopyAsync(string source, string destination, CancellationToken cancellationToken);
    void Move(string source, string destination);
    void Delete(string path);
}

public sealed class SystemArchiveFileSystem : IArchiveFileSystem
{
    public bool Exists(string path) => File.Exists(path);
    public long GetLength(string path) => new FileInfo(path).Length;
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public async Task CopyAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
        await input.CopyToAsync(output, cancellationToken); await output.FlushAsync(cancellationToken);
    }
    public void Move(string source, string destination) => File.Move(source, destination, false);
    public void Delete(string path) => File.Delete(path);
}
