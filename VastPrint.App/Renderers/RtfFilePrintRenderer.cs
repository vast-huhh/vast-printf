using System.Drawing.Printing;
using System.IO;
using System.Runtime.InteropServices;
using VastPrint.App.Interop;
using VastPrint.App.Models;
using VastPrint.App.Services;
using Forms = System.Windows.Forms;

namespace VastPrint.App.Renderers;

public sealed class RtfFilePrintRenderer : IFilePrintRenderer
{
    private readonly PrintDocumentFactory _printDocumentFactory;

    public RtfFilePrintRenderer(PrintDocumentFactory printDocumentFactory)
    {
        _printDocumentFactory = printDocumentFactory;
    }

    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".rtf"];

    public bool CanHandle(string extension)
    {
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public PrintExecutionResult Print(string filePath, AppSettings settings, CancellationToken cancellationToken)
    {
        using var document = _printDocumentFactory.CreatePrintDocument(settings);
        using var richTextBox = new Forms.RichTextBox();
        richTextBox.Rtf = File.ReadAllText(filePath);
        richTextBox.CreateControl();

        document.DocumentName = Path.GetFileName(filePath);
        document.PrintController = new StandardPrintController();

        var firstCharacterOnPage = 0;
        var pageCount = 0;
        document.PrintPage += (_, args) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            pageCount++;

            firstCharacterOnPage = FormatRange(
                richTextBox,
                args,
                firstCharacterOnPage,
                richTextBox.TextLength);

            args.HasMorePages = firstCharacterOnPage < richTextBox.TextLength;
        };

        document.EndPrint += (_, _) => ReleaseCachedPages(richTextBox);
        document.Print();
        return PrintExecutionResult.Completed(pageCount);
    }

    private static int FormatRange(Forms.RichTextBox richTextBox, PrintPageEventArgs args, int charFrom, int charTo)
    {
        var graphics = args.Graphics ?? throw new InvalidOperationException("打印图形上下文不可用。");
        var hdc = graphics.GetHdc();
        var range = new RichTextPrintNative.FormatRange
        {
            Chrg = new RichTextPrintNative.CharRange
            {
                CpMin = charFrom,
                CpMax = charTo,
            },
            Hdc = hdc,
            HdcTarget = hdc,
            Rc = ToTwips(args.MarginBounds),
            RcPage = ToTwips(args.PageBounds),
        };

        var rangePointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<RichTextPrintNative.FormatRange>());

        try
        {
            Marshal.StructureToPtr(range, rangePointer, false);
            var renderedCharacters = RichTextPrintNative.SendFormatRange(richTextBox.Handle, true, rangePointer).ToInt32();
            return renderedCharacters;
        }
        finally
        {
            Marshal.FreeCoTaskMem(rangePointer);
            graphics.ReleaseHdc(hdc);
        }
    }

    private static RichTextPrintNative.Rect ToTwips(System.Drawing.Rectangle rectangle)
    {
        return new RichTextPrintNative.Rect
        {
            Left = (int)(rectangle.Left * 14.4),
            Top = (int)(rectangle.Top * 14.4),
            Right = (int)(rectangle.Right * 14.4),
            Bottom = (int)(rectangle.Bottom * 14.4),
        };
    }

    private static void ReleaseCachedPages(Forms.RichTextBox richTextBox)
    {
        RichTextPrintNative.SendFormatRange(richTextBox.Handle, false, IntPtr.Zero);
    }
}
