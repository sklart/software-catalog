using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Database;
using SoftwareCatalog.Infrastructure.Paths;
using SoftwareCatalog.Scanner;
using SoftwareCatalog.UI.ViewModels;

namespace SoftwareCatalog.UI;
public partial class App : Application
{
    private ServiceProvider? _services;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var services = new ServiceCollection();
        services.AddSingleton<IAppPathService, PortableAppPathService>(); services.AddSingleton<IWritableDirectoryProbe, WritableDirectoryProbe>(); services.AddSingleton<IPortablePathResolver, PortablePathResolver>();
        services.AddSingleton(sp => new CatalogDatabase(sp.GetRequiredService<IAppPathService>().DatabasePath)); services.AddSingleton<IScanCatalogRepository>(sp => sp.GetRequiredService<CatalogDatabase>()); services.AddSingleton<IFileHashCalculator, FileHashCalculator>(); services.AddSingleton(new CatalogScannerOptions()); services.AddSingleton<CatalogScanner>(); services.AddSingleton<MainViewModel>(); services.AddSingleton<MainWindow>(); _services = services.BuildServiceProvider();
        var paths = _services.GetRequiredService<IAppPathService>(); var probe = _services.GetRequiredService<IWritableDirectoryProbe>();
        if (!probe.CanWrite(paths.ApplicationRoot, out _)) { MessageBox.Show("Portable-каталог приложения недоступен для записи.\n\nПереместите Software Catalog в папку, доступную для записи, например D:\\Portable\\SoftwareCatalog.", "Software Catalog", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(); return; }
        paths.EnsureDirectories(); await _services.GetRequiredService<CatalogDatabase>().InitializeAsync(CancellationToken.None); var window = _services.GetRequiredService<MainWindow>(); window.DataContext = _services.GetRequiredService<MainViewModel>(); window.Show();
    }
    protected override void OnExit(ExitEventArgs e) { _services?.Dispose(); base.OnExit(e); }
}
