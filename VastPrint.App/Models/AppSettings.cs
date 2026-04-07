using System.Collections.Generic;

namespace VastPrint.App.Models;

public sealed class AppSettings
{
    public string? SelectedPrinterName { get; set; }

    public bool IncludeSubDirectories { get; set; } = false;

    public bool ContinueOnError { get; set; } = true;

    public PageSetupProfile PageSetup { get; set; } = new();

    public List<string> EnabledExtensions { get; set; } = [];

    public AppSettings Clone()
    {
        return new AppSettings
        {
            SelectedPrinterName = SelectedPrinterName,
            IncludeSubDirectories = IncludeSubDirectories,
            ContinueOnError = ContinueOnError,
            PageSetup = PageSetup.Clone(),
            EnabledExtensions = [.. EnabledExtensions],
        };
    }
}
