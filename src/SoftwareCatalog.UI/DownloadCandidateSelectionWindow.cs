using System.Windows;
using System.Windows.Controls;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI;

public sealed class DownloadCandidateSelectionWindow : Window
{
    private readonly DataGrid _grid;
    public DownloadCandidate? SelectedCandidate => _grid.SelectedItem as DownloadCandidate;
    public DownloadCandidateSelectionWindow(IReadOnlyList<DownloadCandidate> candidates)
    {
        Title = "Выбор installer-файла"; Width = 800; Height = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _grid = new DataGrid { ItemsSource = candidates, AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single, Margin = new Thickness(10) };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Файл", Binding = new System.Windows.Data.Binding(nameof(DownloadCandidate.FileName)) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Версия", Binding = new System.Windows.Data.Binding(nameof(DownloadCandidate.Version)) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Архитектура", Binding = new System.Windows.Data.Binding(nameof(DownloadCandidate.Architecture)) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Тип", Binding = new System.Windows.Data.Binding(nameof(DownloadCandidate.InstallerKind)) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Размер", Binding = new System.Windows.Data.Binding(nameof(DownloadCandidate.Size)) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Провайдер", Binding = new System.Windows.Data.Binding(nameof(DownloadCandidate.Provider)) });
        var download = new Button { Content = "Скачать", IsDefault = true, Margin = new Thickness(10) }; download.Click += (_, _) => { if (SelectedCandidate is not null) DialogResult = true; };
        var cancel = new Button { Content = "Отмена", IsCancel = true, Margin = new Thickness(10) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; buttons.Children.Add(download); buttons.Children.Add(cancel);
        var panel = new DockPanel(); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons); panel.Children.Add(_grid); Content = panel;
    }
}
