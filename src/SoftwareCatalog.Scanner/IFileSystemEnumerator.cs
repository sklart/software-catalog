using System.Collections.Concurrent;

namespace SoftwareCatalog.Scanner;

public interface IFileSystemEnumerator
{
    IEnumerable<string> EnumerateFiles(string root, bool recurse, ConcurrentQueue<ScanError> errors, CancellationToken token, Action markPartial);
}
