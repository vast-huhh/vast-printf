using System.Windows.Interop;
using VastPrint.App.Interop;
using VastPrint.App.Models;
using VastPrint.App.ViewModels;
using VastPrint.App.Views;
using Forms = System.Windows.Forms;

namespace VastPrint.App.Services;

public sealed class PrinterDialogService
{
    private readonly PrintDocumentFactory _printDocumentFactory;

    public PrinterDialogService(PrintDocumentFactory printDocumentFactory)
    {
        _printDocumentFactory = printDocumentFactory;
    }

    public IReadOnlyList<string> GetInstalledPrinters()
    {
        return _printDocumentFactory.GetInstalledPrinters();
    }

    public string? GetDefaultPrinterName()
    {
        return _printDocumentFactory.GetDefaultPrinterName();
    }

    public string DescribePageSetup(PageSetupProfile pageSetup)
    {
        return _printDocumentFactory.Describe(pageSetup);
    }

    public void EnsurePrinterSelection(AppSettings settings)
    {
        _printDocumentFactory.EnsurePrinterSelection(settings);
    }

    public bool ShowPrinterProperties(AppSettings settings)
    {
        using var document = _printDocumentFactory.CreatePrintDocument(settings);
        var ownerHandle = System.Windows.Application.Current.MainWindow is null
            ? IntPtr.Zero
            : new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;

        if (!PrinterPreferencesNative.ShowDialog(ownerHandle, document))
        {
            return false;
        }

        UpdateSettings(document, settings);
        return true;
    }

    public bool ShowPageSetup(AppSettings settings)
    {
        _printDocumentFactory.EnsurePrinterSelection(settings);

        var viewModel = new PageSetupViewModel(
            settings.SelectedPrinterName,
            settings.PageSetup,
            _printDocumentFactory.SupportsColor(settings.SelectedPrinterName),
            _printDocumentFactory.CanDuplex(settings.SelectedPrinterName),
            _printDocumentFactory.Describe,
            profile => ShowNativePageSetup(settings, profile));

        var window = new PageSetupWindow
        {
            Owner = System.Windows.Application.Current.MainWindow,
            DataContext = viewModel,
        };

        if (window.ShowDialog() != true)
        {
            return false;
        }

        settings.PageSetup = viewModel.BuildPageSetup();
        return true;
    }

    private PageSetupProfile? ShowNativePageSetup(AppSettings settings, PageSetupProfile profile)
    {
        using var document = _printDocumentFactory.CreatePrintDocument(settings, profile);
        using var dialog = new Forms.PageSetupDialog
        {
            Document = document,
            AllowMargins = true,
            AllowOrientation = true,
            AllowPaper = true,
            AllowPrinter = true,
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return null;
        }

        return _printDocumentFactory.Capture(document.DefaultPageSettings, document.PrinterSettings, profile);
    }

    private void UpdateSettings(System.Drawing.Printing.PrintDocument document, AppSettings settings)
    {
        settings.SelectedPrinterName = _printDocumentFactory.ResolveInstalledPrinterName(
            document.PrinterSettings.PrinterName,
            settings.SelectedPrinterName);
        settings.PageSetup = _printDocumentFactory.Capture(
            document.DefaultPageSettings,
            document.PrinterSettings,
            settings.PageSetup);
    }
}
