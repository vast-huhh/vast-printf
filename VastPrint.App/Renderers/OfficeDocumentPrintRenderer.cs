using System.IO;
using VastPrint.App.Models;
using VastPrint.App.Services;

namespace VastPrint.App.Renderers;

public sealed class OfficeDocumentPrintRenderer : IFilePrintRenderer
{
    private readonly OfficeDocumentPdfConverter _officeDocumentPdfConverter;
    private readonly PdfFilePrintRenderer _pdfFilePrintRenderer;
    private readonly LogService _logService;

    public OfficeDocumentPrintRenderer(
        OfficeDocumentPdfConverter officeDocumentPdfConverter,
        PdfFilePrintRenderer pdfFilePrintRenderer,
        LogService logService)
    {
        _officeDocumentPdfConverter = officeDocumentPdfConverter;
        _pdfFilePrintRenderer = pdfFilePrintRenderer;
        _logService = logService;
    }

    public IReadOnlyCollection<string> SupportedExtensions { get; } =
    [
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
        ".ppt",
        ".pptx",
    ];

    public bool CanHandle(string extension)
    {
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public PrintExecutionResult Print(string filePath, AppSettings settings, CancellationToken cancellationToken)
    {
        string? tempPdfPath = null;

        try
        {
            tempPdfPath = _officeDocumentPdfConverter.ConvertToPdf(filePath, cancellationToken);
            _logService.Info($"Office renderer forwarding to PDF renderer: file='{filePath}', tempPdf='{tempPdfPath}'");
            return _pdfFilePrintRenderer.Print(tempPdfPath, settings, cancellationToken);
        }
        finally
        {
            if (tempPdfPath is not null)
            {
                try
                {
                    if (File.Exists(tempPdfPath))
                    {
                        File.Delete(tempPdfPath);
                    }
                }
                catch (Exception ex)
                {
                    _logService.Warning($"Failed to delete temporary PDF '{tempPdfPath}': {ex.Message}");
                }
            }
        }
    }
}
