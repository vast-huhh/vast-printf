using System.Collections.ObjectModel;
using System.Windows.Input;
using VastPrint.App.Infrastructure;
using VastPrint.App.Models;
using VastPrint.App.Services;

namespace VastPrint.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private bool _includeSubDirectories;
    private bool _continueOnError;
    private readonly RelayCommand _selectAllExtensionsCommand;
    private readonly RelayCommand _clearAllExtensionsCommand;

    public SettingsViewModel(AppSettings currentSettings, IEnumerable<string> supportedExtensions)
    {
        _includeSubDirectories = currentSettings.IncludeSubDirectories;
        _continueOnError = currentSettings.ContinueOnError;

        var enabledExtensions = new HashSet<string>(currentSettings.EnabledExtensions, StringComparer.OrdinalIgnoreCase);
        var filterOptions = supportedExtensions
            .OrderBy(extension => extension)
            .Select(extension => new ExtensionFilterOption(
                extension,
                ExtensionDescriptionProvider.Describe(extension),
                enabledExtensions.Contains(extension)));

        ExtensionOptions = new ObservableCollection<ExtensionFilterOption>(filterOptions);
        _selectAllExtensionsCommand = new RelayCommand(SelectAllExtensions);
        _clearAllExtensionsCommand = new RelayCommand(ClearAllExtensions);
    }

    public ObservableCollection<ExtensionFilterOption> ExtensionOptions { get; }

    public ICommand SelectAllExtensionsCommand => _selectAllExtensionsCommand;

    public ICommand ClearAllExtensionsCommand => _clearAllExtensionsCommand;

    public bool IncludeSubDirectories
    {
        get => _includeSubDirectories;
        set => SetProperty(ref _includeSubDirectories, value);
    }

    public bool ContinueOnError
    {
        get => _continueOnError;
        set => SetProperty(ref _continueOnError, value);
    }

    public AppSettings BuildSettings(AppSettings originalSettings)
    {
        var updatedSettings = originalSettings.Clone();
        updatedSettings.IncludeSubDirectories = IncludeSubDirectories;
        updatedSettings.ContinueOnError = ContinueOnError;
        updatedSettings.EnabledExtensions = ExtensionOptions
            .Where(option => option.IsEnabled)
            .Select(option => option.Extension)
            .ToList();

        return updatedSettings;
    }

    private void SelectAllExtensions()
    {
        SetAllExtensions(true);
    }

    private void ClearAllExtensions()
    {
        SetAllExtensions(false);
    }

    private void SetAllExtensions(bool isEnabled)
    {
        foreach (var option in ExtensionOptions)
        {
            option.IsEnabled = isEnabled;
        }
    }
}
