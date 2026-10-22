using System.Windows;
using Delivergate.Desktop.Configuration;

namespace Delivergate.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow(AppSettings settings)
    {
        InitializeComponent();
        DataContext = settings;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
