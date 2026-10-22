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

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryViewModel inventory) inventory.BeginCreate();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryViewModel inventory) inventory.BeginEdit();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryViewModel inventory) inventory.CancelEdit();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryViewModel inventory) await inventory.SaveAsync();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not InventoryViewModel inventory || !inventory.CanDelete || inventory.SelectedItem is not { } item) return;
        MessageBoxResult result = MessageBox.Show(Window.GetWindow(this),
            $"Delete item #{item.Id}: {item.Name}?\n\nThis permanently removes the item and cannot be undone in this app.",
            "Confirm item deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        await inventory.DeleteAsync(item.Id, result == MessageBoxResult.Yes);
    }
}
