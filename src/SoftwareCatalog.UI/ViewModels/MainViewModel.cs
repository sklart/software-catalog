using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Scanner;

namespace SoftwareCatalog.UI.ViewModels;
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IScanCatalogRepository _repository; private readonly IPortablePathResolver _paths; private readonly CatalogScanner _scanner; private CancellationTokenSource? _cts; private ScanRoot? _selected; private string _statusText = "Ready";
    public MainViewModel(IScanCatalogRepository repository, IPortablePathResolver paths, CatalogScanner scanner) { _repository = repository; _paths = paths; _scanner = scanner; AddFolderCommand = new AsyncCommand(AddFolderAsync); RemoveFolderCommand = new AsyncCommand(RemoveFolderAsync); ScanCommand = new AsyncCommand(ScanAsync); CancelCommand = new RelayCommand(() => _cts?.Cancel()); _ = LoadAsync(); }
    public ObservableCollection<ScanRoot> ScanRoots { get; } = []; public ObservableCollection<InstallerFile> Files { get; } = []; public ScanRoot? SelectedScanRoot { get => _selected; set { _selected = value; OnChanged(); } } public string StatusText { get => _statusText; private set { _statusText = value; OnChanged(); } } public bool StoreRelativeToApplication { get; set; } public bool IsScanning { get; private set; } public ICommand AddFolderCommand { get; } public ICommand RemoveFolderCommand { get; } public ICommand ScanCommand { get; } public ICommand CancelCommand { get; }
    private async Task LoadAsync() { foreach (var root in await _repository.GetScanRootsAsync(CancellationToken.None)) ScanRoots.Add(root); await RefreshFilesAsync(); }
    private async Task AddFolderAsync() { var dialog = new OpenFolderDialog(); if (dialog.ShowDialog() != true) return; var kind = StoreRelativeToApplication ? ScanRootPathKind.RelativeToApplication : ScanRootPathKind.Absolute; var root = await _repository.AddScanRootAsync(_paths.ToStoredPath(dialog.FolderName, kind), kind, true, CancellationToken.None); ScanRoots.Add(root); SelectedScanRoot = root; }
    private async Task RemoveFolderAsync() { if (SelectedScanRoot is null) return; await _repository.RemoveScanRootAsync(SelectedScanRoot.Id, CancellationToken.None); ScanRoots.Remove(SelectedScanRoot); SelectedScanRoot = null; await RefreshFilesAsync(); }
    private async Task ScanAsync() { if (SelectedScanRoot is null || IsScanning) return; IsScanning = true; _cts = new(); StatusText = "Scanning..."; try { var result = await _scanner.ScanAsync(SelectedScanRoot, new Progress<ScanProgress>(p => StatusText = $"Files: {p.Discovered} | Processed: {p.Processed} | Errors: {p.Errors}"), _cts.Token); StatusText = result.Completed ? $"Completed. Files: {result.ProcessedFiles}; Errors: {result.Errors.Count}" : "Scan cancelled."; await RefreshFilesAsync(); } catch (OperationCanceledException) { StatusText = "Scan cancelled."; } finally { IsScanning = false; _cts.Dispose(); _cts = null; } }
    private async Task RefreshFilesAsync() { Files.Clear(); foreach (var file in await _repository.GetInstallersAsync(CancellationToken.None)) Files.Add(file); }
    public event PropertyChangedEventHandler? PropertyChanged; private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
public sealed class RelayCommand(Action execute) : ICommand { public event EventHandler? CanExecuteChanged { add { } remove { } } public bool CanExecute(object? parameter) => true; public void Execute(object? parameter) => execute(); }
public sealed class AsyncCommand(Func<Task> execute) : ICommand { public event EventHandler? CanExecuteChanged { add { } remove { } } public bool CanExecute(object? parameter) => true; public async void Execute(object? parameter) => await execute(); }
