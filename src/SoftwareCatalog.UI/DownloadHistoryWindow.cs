using System.Windows;
using System.Windows.Controls;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI;

public sealed class DownloadHistoryWindow : Window
{
    public DownloadHistoryWindow(IReadOnlyList<DownloadHistory> entries)
    {
        Title = "История скачиваний"; Width = 980; Height = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var grid = new DataGrid { ItemsSource = entries, AutoGenerateColumns = false, IsReadOnly = true, Margin = new Thickness(10) };
        Add(grid, "Продукт", nameof(DownloadHistory.ProductId)); Add(grid, "Версия", nameof(DownloadHistory.Version)); Add(grid, "Файл", nameof(DownloadHistory.FileName)); Add(grid, "Провайдер", nameof(DownloadHistory.ProviderType)); Add(grid, "Статус", nameof(DownloadHistory.Status)); Add(grid, "Дата", nameof(DownloadHistory.StartedUtc)); Add(grid, "Ошибка", nameof(DownloadHistory.Error)); Content = grid;
    }
    private static void Add(DataGrid grid, string header, string property) => grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new System.Windows.Data.Binding(property) });
}
