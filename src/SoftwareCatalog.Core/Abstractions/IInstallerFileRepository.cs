using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Abstractions;

public interface IInstallerFileRepository
{
    Task<InstallerFile?> FindByPathAsync(string fullPath, CancellationToken cancellationToken);
    Task UpsertAsync(InstallerFile installerFile, CancellationToken cancellationToken);
}
