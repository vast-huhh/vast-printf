using VastPrint.App.Models;
using VastPrint.App.ViewModels;
using VastPrint.App.Views;

namespace VastPrint.App.Services;

public sealed class SettingsDialogService
{
    public AppSettings? Show(AppSettings currentSettings, IReadOnlyCollection<string> supportedExtensions)
    {
        var viewModel = new SettingsViewModel(currentSettings, supportedExtensions);
        var window = new SettingsWindow
        {
            Owner = System.Windows.Application.Current.MainWindow,
            DataContext = viewModel,
        };

        return window.ShowDialog() == true ? viewModel.BuildSettings(currentSettings) : null;
    }
}
