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
}
