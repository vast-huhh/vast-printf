using System.Windows;

namespace VastPrint.App.Views;

public partial class PageSetupWindow : Window
{
    public PageSetupWindow()
    {
        InitializeComponent();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
