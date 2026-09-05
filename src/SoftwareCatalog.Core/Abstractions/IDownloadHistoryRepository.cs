using SoftwareCatalog.Core.Domain;
namespace SoftwareCatalog.Core.Abstractions;
public interface IDownloadHistoryRepository { Task SaveDownloadHistoryAsync(DownloadHistory entry, CancellationToken cancellationToken); Task<IReadOnlyList<DownloadHistory>> GetDownloadHistoryAsync(CancellationToken cancellationToken); Task<bool> HasInstallerSha256Async(string sha256, CancellationToken cancellationToken); }
