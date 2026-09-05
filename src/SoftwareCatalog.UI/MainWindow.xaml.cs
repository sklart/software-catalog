using System.Windows;
using System.Windows.Controls;
using SoftwareCatalog.UI.ViewModels;
namespace SoftwareCatalog.UI;
public partial class MainWindow : Window
{
    public MainWindow() { InitializeComponent(); Loaded += (_, _) => { AddDownloadButton(); AddArchiveControls(); }; }
    private void AddDownloadButton()
    {
        if (DataContext is not MainViewModel viewModel) return;
        var products = FindGroup(this, "Продукты"); var dock = products?.Content as DockPanel; var panel = dock?.Children.OfType<StackPanel>().FirstOrDefault();
        if (dock is null || panel is null || panel.Children.OfType<Button>().Any(button => Equals(button.Command, viewModel.DownloadUpdateCommand))) return;
        panel.Children.Insert(Math.Min(3, panel.Children.Count), new Button { Content = "Скачать обновление", Command = viewModel.DownloadUpdateCommand, Margin = new Thickness(8, 0, 0, 0) });
        panel.Children.Insert(Math.Min(4, panel.Children.Count), new Button { Content = "История скачиваний", Command = viewModel.DownloadHistoryCommand, Margin = new Thickness(8, 0, 0, 0) });
        panel.Children.Insert(Math.Min(5, panel.Children.Count), new Button { Content = "Открыть папку", Command = viewModel.OpenDownloadFolderCommand, Margin = new Thickness(8, 0, 0, 0) });
        var retention = new Button { Content = "Анализ архива", Margin = new Thickness(8, 0, 0, 0) }; retention.Click += (_, _) => { viewModel.RetentionPreviewCommand.Execute(null); ShowRetention(viewModel); }; panel.Children.Add(retention);
        var progressPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0), Tag = "download-progress" }; var bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 14 }; bar.SetBinding(ProgressBar.ValueProperty, new System.Windows.Data.Binding(nameof(MainViewModel.DownloadPercent))); var text = new TextBlock(); text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(MainViewModel.DownloadProgressText))); progressPanel.Children.Add(text); progressPanel.Children.Add(bar); DockPanel.SetDock(progressPanel, Dock.Bottom); dock.Children.Add(progressPanel);
    }
    private void AddArchiveControls()
    {
        if (DataContext is not MainViewModel viewModel) return;
        var history = FindGroup(this, "Файлы выбранного продукта"); var files = history?.Content as DataGrid;
        if (history is null || files is null || history.Content is DockPanel) return;
        files.SetBinding(DataGrid.SelectedItemProperty, new System.Windows.Data.Binding(nameof(MainViewModel.SelectedFile)) { Mode = System.Windows.Data.BindingMode.TwoWay });
        files.Columns.Add(new DataGridTextColumn { Header = "Состояние", Binding = new System.Windows.Data.Binding("StorageState") }); files.Columns.Add(new DataGridCheckBoxColumn { Header = "Закреплён", Binding = new System.Windows.Data.Binding("IsPinned") }); files.Columns.Add(new DataGridTextColumn { Header = "SHA-256", Binding = new System.Windows.Data.Binding("Sha256") }); files.Columns.Add(new DataGridTextColumn { Header = "Последнее обнаружение", Binding = new System.Windows.Data.Binding("LastSeenUtc") });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        actions.Children.Add(new Button { Content = "Архивировать", Command = viewModel.ArchiveCommand }); actions.Children.Add(new Button { Content = "Восстановить", Command = viewModel.RestoreCommand, Margin = new Thickness(8,0,0,0) }); actions.Children.Add(new Button { Content = "В корзину", Command = viewModel.TrashCommand, Margin = new Thickness(8,0,0,0) }); actions.Children.Add(new Button { Content = "Закрепить / открепить", Command = viewModel.PinCommand, Margin = new Thickness(8,0,0,0) }); actions.Children.Add(new Button { Content = "Корзина", Margin = new Thickness(8,0,0,0) }); ((Button)actions.Children[^1]).Click += (_, _) => ShowTrash(viewModel);
        var dock = new DockPanel(); DockPanel.SetDock(actions, Dock.Top); dock.Children.Add(actions); dock.Children.Add(files); history.Content = dock;
    }
    private void ShowTrash(MainViewModel viewModel)
    {
        var entries = viewModel.Files.Where(file => file.StorageState == SoftwareCatalog.Core.Domain.InstallerStorageState.Trashed).ToList(); var grid = new DataGrid { ItemsSource = entries, AutoGenerateColumns = false, IsReadOnly = true, MinHeight = 260 }; grid.Columns.Add(new DataGridTextColumn { Header = "Продукт", Binding = new System.Windows.Data.Binding("ProductName") }); grid.Columns.Add(new DataGridTextColumn { Header = "Версия", Binding = new System.Windows.Data.Binding("ProductVersion") }); grid.Columns.Add(new DataGridTextColumn { Header = "Файл", Binding = new System.Windows.Data.Binding("FileName") }); grid.Columns.Add(new DataGridTextColumn { Header = "Размер", Binding = new System.Windows.Data.Binding("Size") }); grid.Columns.Add(new DataGridTextColumn { Header = "Удалён", Binding = new System.Windows.Data.Binding("StorageChangedUtc") }); grid.Columns.Add(new DataGridTextColumn { Header = "Исходный путь", Binding = new System.Windows.Data.Binding("OriginalRelativePath") }); grid.Columns.Add(new DataGridTextColumn { Header = "SHA-256", Binding = new System.Windows.Data.Binding("Sha256") });
        var restore = new Button { Content = "Восстановить" }; var purge = new Button { Content = "Удалить окончательно", Margin = new Thickness(8,0,0,0) }; restore.Click += (_, _) => { if (grid.SelectedItem is SoftwareCatalog.Core.Domain.InstallerFile file) { viewModel.SelectedFile = file; viewModel.RestoreCommand.Execute(null); } }; purge.Click += (_, _) => { if (grid.SelectedItem is SoftwareCatalog.Core.Domain.InstallerFile file) { viewModel.SelectedFile = file; viewModel.PurgeCommand.Execute(null); } };
        var panel = new DockPanel { Margin = new Thickness(12) }; var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,8,0,0) }; buttons.Children.Add(restore); buttons.Children.Add(purge); DockPanel.SetDock(buttons,Dock.Bottom); panel.Children.Add(buttons); panel.Children.Add(grid); new Window { Title = "Корзина", Content = panel, Owner = this, Width = 1000, Height = 420 }.ShowDialog();
    }
    private void ShowRetention(MainViewModel viewModel)
    {
        var grid = new DataGrid { ItemsSource = viewModel.RetentionItems, AutoGenerateColumns = false, MinHeight = 280 }; grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Применить", Binding = new System.Windows.Data.Binding("Selected") { Mode = System.Windows.Data.BindingMode.TwoWay } }); grid.Columns.Add(new DataGridTextColumn { Header = "Действие", Binding = new System.Windows.Data.Binding("Action") }); grid.Columns.Add(new DataGridTextColumn { Header = "Причина", Binding = new System.Windows.Data.Binding("Reason") }); grid.Columns.Add(new DataGridTextColumn { Header = "Файл", Binding = new System.Windows.Data.Binding("Installer.FileName") }); grid.Columns.Add(new DataGridTextColumn { Header = "Версия", Binding = new System.Windows.Data.Binding("Installer.ProductVersion") }); grid.Columns.Add(new DataGridTextColumn { Header = "Размер", Binding = new System.Windows.Data.Binding("Installer.Size") });
        var apply = new Button { Content = "Применить выбранное", Command = viewModel.ApplyRetentionCommand }; var panel = new DockPanel { Margin = new Thickness(12) }; DockPanel.SetDock(apply,Dock.Bottom); apply.Margin = new Thickness(0,8,0,0); panel.Children.Add(apply); panel.Children.Add(grid); new Window { Title = "Анализ архива", Content = panel, Owner = this, Width = 900, Height = 500 }.ShowDialog();
    }
    private static GroupBox? FindGroup(DependencyObject node, string header)
    {
        if (node is GroupBox box && Equals(box.Header, header)) return box;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++) { var found = FindGroup(System.Windows.Media.VisualTreeHelper.GetChild(node, i), header); if (found is not null) return found; }
        return null;
    }
}
