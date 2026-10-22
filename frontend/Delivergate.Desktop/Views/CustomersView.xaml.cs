using System.Windows.Controls;
using System.Windows;
using Delivergate.Desktop.ViewModels;

namespace Delivergate.Desktop.Views;

public partial class CustomersView : UserControl
{
    public CustomersView() => InitializeComponent();

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomersViewModel customers) await customers.LoadAsync();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomersViewModel customers) customers.ClearSearch();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomersViewModel customers) customers.BeginCreate();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomersViewModel customers) customers.BeginEdit();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomersViewModel customers) customers.CancelEdit();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomersViewModel customers) await customers.SaveAsync();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CustomersViewModel customers || !customers.CanDelete || customers.SelectedCustomer is not { } customer) return;
        MessageBoxResult result = MessageBox.Show(Window.GetWindow(this),
            $"Delete customer #{customer.Id}: {customer.Name}?\n\nThis permanently removes the customer and cannot be undone in this app.",
            "Confirm customer deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        await customers.DeleteAsync(customer.Id, result == MessageBoxResult.Yes);
    }
}
