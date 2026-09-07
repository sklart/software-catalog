using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI;
public sealed class AliasManagementWindow : Window
{
    private readonly ObservableCollection<ProductAlias> _items;
    public AliasManagementWindow(IReadOnlyList<ProductAlias> aliases, Func<string,Task<ProductAlias>> add, Func<ProductAlias,Task> remove)
    {
        var actions=new AliasManagementActions(add,remove); Title="Aliases"; Width=560; Height=400; Owner=Application.Current.MainWindow; _items=new(aliases); var grid=new DataGrid{ItemsSource=_items,AutoGenerateColumns=false,SelectionMode=DataGridSelectionMode.Single};grid.Columns.Add(new DataGridTextColumn{Header="Alias",Binding=new System.Windows.Data.Binding("Alias")});grid.Columns.Add(new DataGridTextColumn{Header="Источник",Binding=new System.Windows.Data.Binding("Source")});var input=new TextBox{Width=240};var addButton=new Button{Content="Добавить",Margin=new Thickness(8,0,0,0)};addButton.Click+=async(_,_)=>{var value=input.Text.Trim();if(value.Length==0)return;_items.Add(await actions.AddAsync(value));input.Clear();};var removeButton=new Button{Content="Удалить выбранный",Margin=new Thickness(8,0,0,0)};removeButton.Click+=async(_,_)=>{if(grid.SelectedItem is ProductAlias alias){await actions.RemoveAsync(alias);_items.Remove(alias);}};var controls=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)};controls.Children.Add(input);controls.Children.Add(addButton);controls.Children.Add(removeButton);var panel=new DockPanel{Margin=new Thickness(12)};DockPanel.SetDock(controls,Dock.Bottom);panel.Children.Add(controls);panel.Children.Add(grid);Content=panel;
    }
}
