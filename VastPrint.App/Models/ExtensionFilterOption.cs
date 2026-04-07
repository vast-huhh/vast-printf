using VastPrint.App.Infrastructure;

namespace VastPrint.App.Models;

public sealed class ExtensionFilterOption : ObservableObject
{
    private bool _isEnabled;

    public ExtensionFilterOption(string extension, string description, bool isEnabled)
    {
        Extension = extension;
        Description = description;
        _isEnabled = isEnabled;
    }

    public string Extension { get; }

    public string Description { get; }

    public string DisplayText => $"{Extension}  {Description}";

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}
