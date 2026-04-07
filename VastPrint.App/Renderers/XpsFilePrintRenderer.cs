using System.IO;
using System.Printing;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using VastPrint.App.Models;
using VastPrint.App.Services;

namespace VastPrint.App.Renderers;

public sealed class XpsFilePrintRenderer : IFilePrintRenderer
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".xps"];

    public bool CanHandle(string extension)
    {
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public PrintExecutionResult Print(string filePath, AppSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(settings.SelectedPrinterName))
        {
            return PrintExecutionResult.Failed("未选择可用打印机。");
        }

        using var xpsDocument = new XpsDocument(filePath, FileAccess.Read);
        var documentSequence = xpsDocument.GetFixedDocumentSequence();

        if (documentSequence is null)
        {
            return PrintExecutionResult.Failed("无法读取 XPS 文档内容。");
        }

        using var printServer = new LocalPrintServer();
        using var printQueue = printServer.GetPrintQueue(settings.SelectedPrinterName);
        var writer = PrintQueue.CreateXpsDocumentWriter(printQueue);
        writer.Write(documentSequence);

        var pageCount = documentSequence.DocumentPaginator.PageCount;
        return PrintExecutionResult.Completed(pageCount > 0 ? pageCount : 0);
    }
}
