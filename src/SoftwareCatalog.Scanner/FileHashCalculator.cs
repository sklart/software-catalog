using System.Security.Cryptography;

namespace SoftwareCatalog.Scanner;

public interface IFileHashCalculator { Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken); }
public sealed class FileHashCalculator : IFileHashCalculator
{
    public async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}
