using System.Windows;
using Delivergate.Desktop.ViewModels;

namespace Delivergate.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void CheckConnection_Click(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.CheckConnectionAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        string password = LoginPassword.Password;
        LoginPassword.Clear();
        // Never store the password as a view-model property or write it to disk.
        await _viewModel.Auth.LoginAsync(password);
    }

    private async void VerifySession_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.Auth.VerifySessionAsync();
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        LoginPassword.Clear();
        _viewModel.Auth.Logout();
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e) =>
        _viewModel.Workspace.Navigate(WorkspacePage.Dashboard);

    private async void Inventory_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Workspace.Navigate(WorkspacePage.Inventory))
            await _viewModel.Inventory.LoadAsync();
    }

    private void Customers_Click(object sender, RoutedEventArgs e) =>
        _viewModel.Workspace.Navigate(WorkspacePage.Customers);
}
