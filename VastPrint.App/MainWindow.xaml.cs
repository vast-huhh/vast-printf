using System.Windows;
using System.Linq;
using VastPrint.App.Models;
using VastPrint.App.ViewModels;

namespace VastPrint.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        e.Effects = System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (DataContext is MainViewModel viewModel &&
            e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] droppedPaths)
        {
            viewModel.HandleDroppedPaths(droppedPaths);
        }
    }

    private void QueueDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel ||
            sender is not System.Windows.Controls.DataGrid dataGrid)
        {
            return;
        }

        viewModel.UpdateSelectedQueueItems(dataGrid.SelectedItems.OfType<PrintQueueItem>());
    }
}
