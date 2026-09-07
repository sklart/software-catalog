using System.Windows;
using System.Windows.Controls;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.UI;

public sealed class UpdateCandidateSelectionWindow : Window
{
    private readonly DataGrid _grid;
    public UpdateCandidate? SelectedCandidate => _grid.SelectedItem as UpdateCandidate;
    public UpdateCandidateSelectionWindow(IReadOnlyList<UpdateCandidate> candidates)
    {
        Title="Кандидаты обновления"; Width=1050; Height=460; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        _grid=new DataGrid { ItemsSource=candidates, AutoGenerateColumns=false, IsReadOnly=true, SelectionMode=DataGridSelectionMode.Single, MinHeight=320 };
        foreach(var column in new[] { ("Provider","ProviderType"),("External ID","ExternalId"),("Display name","DisplayName"),("Publisher/Owner","PublisherOrOwner"),("Latest version","LatestVersion"),("Confidence","Confidence"),("Reason","Reason") }) _grid.Columns.Add(new DataGridTextColumn { Header=column.Item1, Binding=new System.Windows.Data.Binding(column.Item2) });
        var bind=new Button { Content="Привязать", IsDefault=true, MinWidth=100 }; bind.Click+=(_,_)=>{if(SelectedCandidate is not null)DialogResult=true;}; var cancel=new Button { Content="Отмена", IsCancel=true, MinWidth=100, Margin=new Thickness(8,0,0,0) }; var buttons=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,8,0,0) };buttons.Children.Add(bind);buttons.Children.Add(cancel);var panel=new DockPanel { Margin=new Thickness(12) };DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);panel.Children.Add(_grid);Content=panel;
    }
}
