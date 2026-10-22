using System.Windows.Controls;
using System.Windows;
using Delivergate.Desktop.ViewModels;

namespace Delivergate.Desktop.Views;

public partial class InventoryView : UserControl
{
    public InventoryView() => InitializeComponent();

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryViewModel inventory) await inventory.LoadAsync();
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryViewModel inventory) inventory.ClearFilters();
    }
}
