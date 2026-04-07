using VastPrint.App.Renderers;
using VastPrint.App.Services;
using VastPrint.App.ViewModels;

namespace VastPrint.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsService = new SettingsService();
        var printDocumentFactory = new PrintDocumentFactory();
        var filePickerService = new FilePickerService();
        var fileDiscoveryService = new FileDiscoveryService();
        var settingsDialogService = new SettingsDialogService();
        var printerDialogService = new PrinterDialogService(printDocumentFactory);
        var printHistoryService = new PrintHistoryService();
        var logService = new LogService();
        var officeDocumentPdfConverter = new OfficeDocumentPdfConverter(logService);
        var pdfFilePrintRenderer = new PdfFilePrintRenderer(printDocumentFactory, logService);
        var printExecutionService = new PrintExecutionService(
        [
            pdfFilePrintRenderer,
            new OfficeDocumentPrintRenderer(officeDocumentPdfConverter, pdfFilePrintRenderer, logService),
            new ImageFilePrintRenderer(printDocumentFactory),
            new TextFilePrintRenderer(printDocumentFactory),
            new RtfFilePrintRenderer(printDocumentFactory),
            new XpsFilePrintRenderer(),
        ], logService);

        var viewModel = new MainViewModel(
            settingsService,
            filePickerService,
            fileDiscoveryService,
            settingsDialogService,
            printerDialogService,
            printExecutionService,
            printHistoryService,
            logService);

        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        MainWindow = window;
        window.Show();
    }
}
