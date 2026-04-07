using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using VastPrint.App.Models;
using VastPrint.App.Services;

namespace VastPrint.App.Renderers;

public sealed class TextFilePrintRenderer : IFilePrintRenderer
{
    private readonly PrintDocumentFactory _printDocumentFactory;

    public TextFilePrintRenderer(PrintDocumentFactory printDocumentFactory)
    {
        _printDocumentFactory = printDocumentFactory;
    }

    public IReadOnlyCollection<string> SupportedExtensions { get; } =
    [
        ".txt",
        ".log",
        ".csv",
        ".json",
        ".xml",
        ".md",
        ".ini",
    ];

    public bool CanHandle(string extension)
    {
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public PrintExecutionResult Print(string filePath, AppSettings settings, CancellationToken cancellationToken)
    {
        var lines = LoadLines(filePath);
        using var document = _printDocumentFactory.CreatePrintDocument(settings);
        using var font = new Font("Microsoft YaHei UI", 10f);
        using var brush = new SolidBrush(Color.Black);
        using var stringFormat = new StringFormat
        {
            Trimming = StringTrimming.None,
        };

        document.DocumentName = Path.GetFileName(filePath);
        document.PrintController = new StandardPrintController();

        var currentLineIndex = 0;
        var pageCount = 0;
        document.PrintPage += (_, args) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            pageCount++;

            var graphics = args.Graphics ?? throw new InvalidOperationException("打印图形上下文不可用。");
            var top = (float)args.MarginBounds.Top;
            var lineHeight = font.GetHeight(graphics) + 2f;
            var drawableWidth = args.MarginBounds.Width;
            var maxBottom = (float)args.MarginBounds.Bottom;

            while (currentLineIndex < lines.Count)
            {
                var textLine = string.IsNullOrEmpty(lines[currentLineIndex]) ? " " : lines[currentLineIndex];
                var measuredHeight = Math.Max(
                    lineHeight,
                    graphics.MeasureString(textLine, font, drawableWidth).Height);

                if (top + measuredHeight > maxBottom)
                {
                    break;
                }

                graphics.DrawString(
                    textLine,
                    font,
                    brush,
                    new RectangleF(args.MarginBounds.Left, top, drawableWidth, measuredHeight),
                    stringFormat);

                top += measuredHeight;
                currentLineIndex++;
            }

            args.HasMorePages = currentLineIndex < lines.Count;
        };

        document.Print();
        return PrintExecutionResult.Completed(pageCount);
    }

    private static IReadOnlyList<string> LoadLines(string filePath)
    {
        try
        {
            using var streamReader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
            var text = streamReader.ReadToEnd();
            return SplitLines(text);
        }
        catch
        {
            var fallbackText = File.ReadAllText(filePath, Encoding.Default);
            return SplitLines(fallbackText);
        }
    }

    private static IReadOnlyList<string> SplitLines(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return normalized.Split('\n').ToList();
    }
}
