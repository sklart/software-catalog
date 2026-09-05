using System.Windows;
using System.Windows.Controls;
using SoftwareCatalog.UI.ViewModels;
namespace SoftwareCatalog.UI;
public partial class MainWindow : Window
{
    public MainWindow() { InitializeComponent(); Loaded += (_, _) => AddDownloadButton(); }
    private void AddDownloadButton()
    {
        if (DataContext is not MainViewModel viewModel) return;
        var products = FindGroup(this, "Продукты"); var dock = products?.Content as DockPanel; var panel = dock?.Children.OfType<StackPanel>().FirstOrDefault();
        if (dock is null || panel is null || panel.Children.OfType<Button>().Any(button => Equals(button.Command, viewModel.DownloadUpdateCommand))) return;
        panel.Children.Insert(Math.Min(3, panel.Children.Count), new Button { Content = "Скачать обновление", Command = viewModel.DownloadUpdateCommand, Margin = new Thickness(8, 0, 0, 0) });
        panel.Children.Insert(Math.Min(4, panel.Children.Count), new Button { Content = "История скачиваний", Command = viewModel.DownloadHistoryCommand, Margin = new Thickness(8, 0, 0, 0) });
        panel.Children.Insert(Math.Min(5, panel.Children.Count), new Button { Content = "Открыть папку", Command = viewModel.OpenDownloadFolderCommand, Margin = new Thickness(8, 0, 0, 0) });
        var progressPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0), Tag = "download-progress" }; var bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 14 }; bar.SetBinding(ProgressBar.ValueProperty, new System.Windows.Data.Binding(nameof(MainViewModel.DownloadPercent))); var text = new TextBlock(); text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(MainViewModel.DownloadProgressText))); progressPanel.Children.Add(text); progressPanel.Children.Add(bar); DockPanel.SetDock(progressPanel, Dock.Bottom); dock.Children.Add(progressPanel);
    }
    private static GroupBox? FindGroup(DependencyObject node, string header)
    {
        if (node is GroupBox box && Equals(box.Header, header)) return box;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++) { var found = FindGroup(System.Windows.Media.VisualTreeHelper.GetChild(node, i), header); if (found is not null) return found; }
        return null;
    }
}
