using SoftwareCatalog.Core.Domain;
namespace SoftwareCatalog.Core.Tests;
public sealed class InstallerFileMetadataTests { [Fact] public void UsesLongIdentity() { var now = DateTimeOffset.UtcNow; var file = new InstallerFile(1, 2, "tool.exe", "tool.exe", ".exe", 10, now, "ABC", now, now, true); Assert.Equal(1, file.Id); Assert.Equal(2, file.ScanRootId); } }
