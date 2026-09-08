using System.Windows;
using System.Windows.Controls;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI;

public sealed class ProductSelectionWindow : Window
{
    private readonly DataGrid _grid;
    public SoftwareProduct? SelectedProduct => _grid.SelectedItem as SoftwareProduct;
    public ProductSelectionWindow(IEnumerable<SoftwareProduct> products, SoftwareProduct? current)
    {
        Title = "Выбор продукта"; Width = 760; Height = 480; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _grid = new DataGrid { ItemsSource = ProductSelectionWorkflow.Order(products), SelectedItem = current, AutoGenerateColumns = false, IsReadOnly = true, MinHeight = 360 };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Продукт", Binding = new System.Windows.Data.Binding(nameof(SoftwareProduct.CanonicalName)) }); _grid.Columns.Add(new DataGridTextColumn { Header = "Издатель", Binding = new System.Windows.Data.Binding(nameof(SoftwareProduct.Publisher)) }); _grid.Columns.Add(new DataGridTextColumn { Header = "Последняя версия", Binding = new System.Windows.Data.Binding(nameof(SoftwareProduct.LatestVersion)) });
        var select = new Button { Content = "Привязать", IsDefault = true }; select.Click += (_, _) => { if (ProductSelectionWorkflow.Confirm(SelectedProduct) is not null) DialogResult = true; }; var cancel = new Button { Content = "Отмена", IsCancel = true, Margin = new Thickness(8, 0, 0, 0) }; cancel.Click += (_, _) => { _ = ProductSelectionWorkflow.Cancel(); DialogResult = false; }; var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) }; buttons.Children.Add(select); buttons.Children.Add(cancel); var panel = new DockPanel { Margin = new Thickness(12) }; DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons); panel.Children.Add(_grid); Content = panel;
    }
}
