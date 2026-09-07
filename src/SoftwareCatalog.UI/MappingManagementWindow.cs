using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI;
public sealed class MappingManagementWindow : Window
{
    private readonly DataGrid _grid; private readonly ObservableCollection<ProductUpdateSource> _items;
    public MappingManagementWindow(IReadOnlyList<ProductUpdateSource> mappings, DateTimeOffset? lastCheckedUtc, Func<ProductUpdateSource,Task> save, Func<ProductUpdateSource,Task> remove)
    {
        Title="Mappings"; Width=1150; Height=450; Owner=Application.Current.MainWindow; _items=new(mappings); _grid=new DataGrid { ItemsSource=_items, AutoGenerateColumns=false, SelectionMode=DataGridSelectionMode.Single, MinHeight=300 };
        foreach(var column in new[]{("Provider","ProviderType",true),("External ID","ExternalId",true),("Source","Source",true),("Confidence","Confidence",true),("Enabled","Enabled",true),("CreatedUtc","CreatedUtc",true),("UpdatedUtc","UpdatedUtc",true)}) _grid.Columns.Add(column.Item2=="Enabled" ? new DataGridCheckBoxColumn{Header=column.Item1,Binding=new System.Windows.Data.Binding(column.Item2),IsReadOnly=true} : new DataGridTextColumn{Header=column.Item1,Binding=new System.Windows.Data.Binding(column.Item2),IsReadOnly=column.Item3});
        _grid.Columns.Add(new DataGridTextColumn { Header="Last check", Binding=new System.Windows.Data.Binding { Source=lastCheckedUtc, StringFormat="u" }, IsReadOnly=true });
        var externalId=new TextBox { Width=280 }; _grid.SelectionChanged+=(_,_)=>{if(_grid.SelectedItem is ProductUpdateSource item)externalId.Text=item.ExternalId;};
        var enable=new Button{Content="Enable"}; enable.Click+=async(_,_)=>{if(_grid.SelectedItem is ProductUpdateSource item){var value=item with{Enabled=true};await save(value);Replace(item,value);}}; var disable=new Button{Content="Disable",Margin=new Thickness(8,0,0,0)};disable.Click+=async(_,_)=>{if(_grid.SelectedItem is ProductUpdateSource item){var value=item with{Enabled=false};await save(value);Replace(item,value);}};var change=new Button{Content="Change",Margin=new Thickness(8,0,0,0)};change.Click+=async(_,_)=>{if(_grid.SelectedItem is ProductUpdateSource item && !string.IsNullOrWhiteSpace(externalId.Text)){var value=item with { ExternalId=externalId.Text.Trim() };await save(value);Replace(item,value);}};var delete=new Button{Content="Remove",Margin=new Thickness(8,0,0,0)};delete.Click+=async(_,_)=>{if(_grid.SelectedItem is ProductUpdateSource item){await remove(item);_items.Remove(item);}};var close=new Button{Content="Закрыть",IsCancel=true,Margin=new Thickness(8,0,0,0)};var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,8,0,0)};buttons.Children.Add(new TextBlock{Text="External ID:",VerticalAlignment=VerticalAlignment.Center});buttons.Children.Add(externalId);foreach(var button in new[]{enable,disable,change,delete,close})buttons.Children.Add(button);var panel=new DockPanel{Margin=new Thickness(12)};DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);panel.Children.Add(_grid);Content=panel;
    }
    private void Replace(ProductUpdateSource oldItem,ProductUpdateSource newItem){_items[_items.IndexOf(oldItem)]=newItem;_grid.SelectedItem=newItem;}
}
