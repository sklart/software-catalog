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
using SoftwareCatalog.Core;
using SoftwareCatalog.Providers;
using System.Net.Http;

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
        services.AddSingleton(settings); services.AddSingleton<SettingsService>(); services.AddSingleton(sp => new PortableLogger(sp.GetRequiredService<IAppPathService>(), settings.LogRetention)); services.AddSingleton<IAppLogger>(sp => sp.GetRequiredService<PortableLogger>()); services.AddSingleton(sp => new CatalogDatabase(sp.GetRequiredService<IAppPathService>().DatabasePath, sp.GetRequiredService<IAppLogger>())); services.AddSingleton<IScanCatalogRepository>(sp => sp.GetRequiredService<CatalogDatabase>()); services.AddSingleton<IProductCatalogRepository>(sp => sp.GetRequiredService<CatalogDatabase>()); services.AddSingleton<IFileHashCalculator, FileHashCalculator>(); services.AddSingleton(new CatalogScannerOptions { MaxDegreeOfParallelism = settings.MaxParallelism, SupportedExtensions = new HashSet<string>(settings.SupportedExtensions, StringComparer.OrdinalIgnoreCase) }); services.AddSingleton<CatalogScanner>(); services.AddSingleton<ProductNormalizer>(); services.AddSingleton<ProductMatchingService>(); services.AddSingleton<VersionComparer>(); services.AddSingleton<ProductCatalogService>(); services.AddSingleton(new HttpClient { BaseAddress = new Uri("https://api.github.com/"), Timeout = TimeSpan.FromSeconds(15), DefaultRequestHeaders = { UserAgent = { new System.Net.Http.Headers.ProductInfoHeaderValue("SoftwareCatalog", "1.0") } } }); services.AddSingleton<IWinGetClient, ProcessWinGetClient>(); services.AddSingleton<IUpdateProvider, WinGetProvider>(); services.AddSingleton<IUpdateProvider, GitHubReleasesProvider>(); services.AddSingleton<UpdateDiscoveryService>(); services.AddSingleton<IUpdateChecker>(sp => sp.GetRequiredService<UpdateDiscoveryService>()); services.AddSingleton<UpdateBatchService>(); services.AddSingleton<MainViewModel>(); services.AddSingleton<MainWindow>(); _services = services.BuildServiceProvider();
        var paths = _services.GetRequiredService<IAppPathService>(); var probe = _services.GetRequiredService<IWritableDirectoryProbe>();
        if (!probe.CanWrite(paths.ApplicationRoot, out _)) { MessageBox.Show("Portable-каталог приложения недоступен для записи.\n\nПереместите Software Catalog в папку, доступную для записи, например D:\\Portable\\SoftwareCatalog.", "Software Catalog", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(); return; }
        paths.EnsureDirectories(); _services.GetRequiredService<PortableLogger>().Information("startup", "Application startup"); await _services.GetRequiredService<CatalogDatabase>().InitializeAsync(CancellationToken.None); var window = _services.GetRequiredService<MainWindow>(); ((ScanRootAvailabilityConverter)window.Resources["Availability"]).Resolver = _services.GetRequiredService<IPortablePathResolver>(); window.DataContext = _services.GetRequiredService<MainViewModel>(); window.Show();
    }
    protected override void OnExit(ExitEventArgs e) { _services?.GetService<PortableLogger>()?.Information("shutdown", "Application shutdown"); _services?.Dispose(); base.OnExit(e); }
}
