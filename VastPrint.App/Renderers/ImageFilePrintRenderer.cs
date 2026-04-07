using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using VastPrint.App.Models;
using VastPrint.App.Services;

namespace VastPrint.App.Renderers;

public sealed class ImageFilePrintRenderer : IFilePrintRenderer
{
    private readonly PrintDocumentFactory _printDocumentFactory;

    public ImageFilePrintRenderer(PrintDocumentFactory printDocumentFactory)
    {
        _printDocumentFactory = printDocumentFactory;
    }

    public IReadOnlyCollection<string> SupportedExtensions { get; } =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff",
    ];

    public bool CanHandle(string extension)
    {
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public PrintExecutionResult Print(string filePath, AppSettings settings, CancellationToken cancellationToken)
    {
        using var image = Image.FromFile(filePath);
        using var document = _printDocumentFactory.CreatePrintDocument(settings);
        document.DocumentName = Path.GetFileName(filePath);
        document.PrintController = new StandardPrintController();

        var pageCount = 0;
        document.PrintPage += (_, args) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            pageCount++;

            var graphics = args.Graphics ?? throw new InvalidOperationException("打印图形上下文不可用。");
            var destination = CalculateDestination(args.MarginBounds, image.Size);
            graphics.DrawImage(image, destination);
            args.HasMorePages = false;
        };

        document.Print();
        return PrintExecutionResult.Completed(pageCount);
    }

    private static RectangleF CalculateDestination(Rectangle marginBounds, Size imageSize)
    {
        var scaleX = (float)marginBounds.Width / imageSize.Width;
        var scaleY = (float)marginBounds.Height / imageSize.Height;
        var scale = Math.Min(scaleX, scaleY);

        var renderedWidth = imageSize.Width * scale;
        var renderedHeight = imageSize.Height * scale;
        var left = marginBounds.Left + ((marginBounds.Width - renderedWidth) / 2f);
        var top = marginBounds.Top + ((marginBounds.Height - renderedHeight) / 2f);

        return new RectangleF(left, top, renderedWidth, renderedHeight);
    }
}
