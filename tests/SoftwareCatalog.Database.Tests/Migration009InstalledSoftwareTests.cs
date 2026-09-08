using Microsoft.Data.Sqlite;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Database;

namespace SoftwareCatalog.Database.Tests;

public sealed class Migration009InstalledSoftwareTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "catalog-m9-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task PersistsInventoryBindingAndMarksMissingEntries()
    {
        Directory.CreateDirectory(_folder); var database = new CatalogDatabase(Path.Combine(_folder,"catalog.db")); await database.InitializeAsync(CancellationToken.None); var now=DateTimeOffset.UtcNow; var item=new InstalledSoftware(0,"Contoso Tool","1.0","Contoso","contosotool","1.0",@"C:\Tool",null,"x64",InstalledSoftwareSource.Hklm64Uninstall,"Contoso.Tool",null,null,null,now,now,true);
        await database.UpsertInstalledSoftwareAsync([item],now,CancellationToken.None); var stored=Assert.Single(await database.GetInstalledSoftwareAsync(CancellationToken.None)); Assert.True(stored.Exists); Assert.Equal("Contoso Tool",stored.DisplayName);
        var product=new SoftwareProduct(Guid.NewGuid(),"Contoso Tool","Contoso","contosotool",now,now); await database.UpsertProductAsync(product,CancellationToken.None); await database.SetInstalledSoftwareBindingAsync(stored.Id,product.Id,InstalledSoftwareMatchSource.Manual,InstalledSoftwareMatchConfidence.Exact,CancellationToken.None);
        stored=Assert.Single(await database.GetInstalledSoftwareAsync(CancellationToken.None)); Assert.Equal(product.Id,stored.ProductId); Assert.Equal(InstalledSoftwareMatchSource.Manual,stored.MatchSource);
        await database.UpsertInstalledSoftwareAsync([],now.AddMinutes(1),CancellationToken.None); Assert.False(Assert.Single(await database.GetInstalledSoftwareAsync(CancellationToken.None)).Exists);
        await database.InitializeAsync(CancellationToken.None); Assert.Single(await database.GetInstalledSoftwareAsync(CancellationToken.None));
        await using var connection=new SqliteConnection($"Data Source={Path.Combine(_folder,"catalog.db")};Pooling=False"); await connection.OpenAsync(); await using var command=connection.CreateCommand(); command.CommandText="SELECT COUNT(*) FROM schema_migrations WHERE version=9"; Assert.Equal(1L,Convert.ToInt64(await command.ExecuteScalarAsync()));
    }
    public void Dispose(){if(Directory.Exists(_folder))Directory.Delete(_folder,true);}
}
