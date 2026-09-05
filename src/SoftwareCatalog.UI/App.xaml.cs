using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Database;
using SoftwareCatalog.Infrastructure.Paths;
using SoftwareCatalog.Infrastructure.Settings;
using SoftwareCatalog.Infrastructure.Logging;
using SoftwareCatalog.Scanner;
using SoftwareCatalog.UI.ViewModels;
using SoftwareCatalog.UI.Converters;

namespace SoftwareCatalog.UI;
public partial class App : Application
{
    private ServiceProvider? _services;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var initialPaths = new PortableAppPathService();
        var settings = await new SettingsService(initialPaths).LoadAsync(CancellationToken.None);
        var services = new ServiceCollection();
        services.AddSingleton<IAppPathService, PortableAppPathService>(); services.AddSingleton<IWritableDirectoryProbe, WritableDirectoryProbe>(); services.AddSingleton<IPortablePathResolver, PortablePathResolver>();
        services.AddSingleton<SettingsService>(); services.AddSingleton(sp => new PortableLogger(sp.GetRequiredService<IAppPathService>(), settings.LogRetention)); services.AddSingleton<IAppLogger>(sp => sp.GetRequiredService<PortableLogger>()); services.AddSingleton(sp => new CatalogDatabase(sp.GetRequiredService<IAppPathService>().DatabasePath, sp.GetRequiredService<IAppLogger>())); services.AddSingleton<IScanCatalogRepository>(sp => sp.GetRequiredService<CatalogDatabase>()); services.AddSingleton<IFileHashCalculator, FileHashCalculator>(); services.AddSingleton(new CatalogScannerOptions { MaxDegreeOfParallelism = settings.MaxParallelism, SupportedExtensions = new HashSet<string>(settings.SupportedExtensions, StringComparer.OrdinalIgnoreCase) }); services.AddSingleton<CatalogScanner>(); services.AddSingleton<MainViewModel>(); services.AddSingleton<MainWindow>(); _services = services.BuildServiceProvider();
        ScanRootAvailabilityConverter.Resolver = _services.GetRequiredService<IPortablePathResolver>(); var paths = _services.GetRequiredService<IAppPathService>(); var probe = _services.GetRequiredService<IWritableDirectoryProbe>();
        if (!probe.CanWrite(paths.ApplicationRoot, out _)) { MessageBox.Show("Portable-каталог приложения недоступен для записи.\n\nПереместите Software Catalog в папку, доступную для записи, например D:\\Portable\\SoftwareCatalog.", "Software Catalog", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(); return; }
        paths.EnsureDirectories(); _services.GetRequiredService<PortableLogger>().Information("startup", "Application startup"); await _services.GetRequiredService<CatalogDatabase>().InitializeAsync(CancellationToken.None); var window = _services.GetRequiredService<MainWindow>(); window.DataContext = _services.GetRequiredService<MainViewModel>(); window.Show();
    }
    protected override void OnExit(ExitEventArgs e) { _services?.GetService<PortableLogger>()?.Information("shutdown", "Application shutdown"); _services?.Dispose(); base.OnExit(e); }
}
