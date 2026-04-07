using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.InteropServices;
using Docnet.Core;
using Docnet.Core.Models;
using VastPrint.App.Models;
using VastPrint.App.Services;

namespace VastPrint.App.Renderers;

public sealed class PdfFilePrintRenderer : IFilePrintRenderer
{
    private const float MinimumMonochromeRenderDpi = 300f;

    private readonly PrintDocumentFactory _printDocumentFactory;
    private readonly LogService _logService;

    public PdfFilePrintRenderer(PrintDocumentFactory printDocumentFactory, LogService logService)
    {
        _printDocumentFactory = printDocumentFactory;
        _logService = logService;
    }

    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".pdf"];

    public bool CanHandle(string extension)
    {
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public PrintExecutionResult Print(string filePath, AppSettings settings, CancellationToken cancellationToken)
    {
        var useMonochromeBitmap = settings.PageSetup.ColorMode == PrintColorMode.BlackAndWhite;
        var configuredColorRenderDpi = Math.Clamp(settings.PageSetup.ColorRenderDpi, 120, 300);
        using var document = _printDocumentFactory.CreatePrintDocument(settings);
        document.DocumentName = Path.GetFileName(filePath);
        document.PrintController = new StandardPrintController();

        var docReader = default(Docnet.Core.Readers.IDocReader);
        var currentPageIndex = 0;
        var totalPages = 0;

        document.PrintPage += (_, args) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var graphics = args.Graphics ?? throw new InvalidOperationException("打印图形上下文不可用。");
            var effectiveDpiX = useMonochromeBitmap
                ? Math.Max(graphics.DpiX, MinimumMonochromeRenderDpi)
                : Math.Min(graphics.DpiX, configuredColorRenderDpi);
            var effectiveDpiY = useMonochromeBitmap
                ? Math.Max(graphics.DpiY, MinimumMonochromeRenderDpi)
                : Math.Min(graphics.DpiY, configuredColorRenderDpi);
            var widthPixels = Math.Max(1, ConvertToPixels(args.MarginBounds.Width, effectiveDpiX));
            var heightPixels = Math.Max(1, ConvertToPixels(args.MarginBounds.Height, effectiveDpiY));
            var pageDimensions = CreatePageDimensions(widthPixels, heightPixels);
            _logService.Info(
                $"Pdf PrintPage start: file='{filePath}', pageIndex={currentPageIndex}, " +
                $"marginBounds={args.MarginBounds.Width}x{args.MarginBounds.Height}, " +
                $"pageBounds={args.PageBounds.Width}x{args.PageBounds.Height}, " +
                $"dpi={graphics.DpiX}x{graphics.DpiY}, effectiveDpi={effectiveDpiX}x{effectiveDpiY}, " +
                $"viewport={Math.Min(widthPixels, heightPixels)}x{Math.Max(widthPixels, heightPixels)}");

            docReader ??= DocLib.Instance.GetDocReader(filePath, pageDimensions);
            totalPages = docReader.GetPageCount();

            using var pageReader = docReader.GetPageReader(currentPageIndex);
            var pageWidth = pageReader.GetPageWidth();
            var pageHeight = pageReader.GetPageHeight();
            _logService.Info(
                $"Pdf page info: file='{filePath}', pageIndex={currentPageIndex}, sourceSize={pageWidth}x{pageHeight}, totalPages={totalPages}");

            var pageImageBytes = pageReader.GetImage(RenderFlags.RenderForPrinting | RenderFlags.RenderAnnotations);

            using var bitmap = CreatePrintableBitmap(pageWidth, pageHeight, pageImageBytes, useMonochromeBitmap);
            _logService.Info(
                $"Pdf bitmap prepared: file='{filePath}', pageIndex={currentPageIndex}, pixelFormat={bitmap.PixelFormat}, monochrome={useMonochromeBitmap}");

            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            graphics.DrawImage(bitmap, args.MarginBounds);

            currentPageIndex++;
            args.HasMorePages = currentPageIndex < totalPages;
        };

        try
        {
            document.Print();
            return PrintExecutionResult.Completed(totalPages > 0 ? totalPages : currentPageIndex);
        }
        catch (Exception ex)
        {
            _logService.Error(
                $"Pdf print exception: file='{filePath}', landscape={settings.PageSetup.Landscape}, " +
                $"paper='{settings.PageSetup.PaperName ?? "<null>"}', paperSize={settings.PageSetup.PaperWidth}x{settings.PageSetup.PaperHeight}, details={ex}");
            throw;
        }
        finally
        {
            docReader?.Dispose();
        }
    }

    private static PageDimensions CreatePageDimensions(int widthPixels, int heightPixels)
    {
        var smallerDimension = Math.Min(widthPixels, heightPixels);
        var largerDimension = Math.Max(widthPixels, heightPixels);
        return new PageDimensions(smallerDimension, largerDimension);
    }

    private static int ConvertToPixels(int hundredthsOfAnInch, float dpi)
    {
        return (int)Math.Round(hundredthsOfAnInch * dpi / 100f);
    }

    private static Bitmap CreatePrintableBitmap(int width, int height, byte[] bgraBytes, bool monochrome)
    {
        var colorBitmap = CreateColorBitmap(width, height, bgraBytes);
        if (!monochrome)
        {
            return colorBitmap;
        }

        using (colorBitmap)
        {
            return ConvertToMonochrome(colorBitmap);
        }
    }

    private static Bitmap CreateColorBitmap(int width, int height, byte[] bgraBytes)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format24bppRgb);

        try
        {
            var targetStride = bitmapData.Stride;
            var targetBytes = new byte[Math.Abs(targetStride) * height];

            for (var y = 0; y < height; y++)
            {
                var sourceRow = y * width * 4;
                var targetRow = targetStride > 0 ? y * targetStride : (height - 1 - y) * -targetStride;

                for (var x = 0; x < width; x++)
                {
                    var sourceIndex = sourceRow + (x * 4);
                    var targetIndex = targetRow + (x * 3);
                    var blue = bgraBytes[sourceIndex];
                    var green = bgraBytes[sourceIndex + 1];
                    var red = bgraBytes[sourceIndex + 2];
                    var alpha = bgraBytes[sourceIndex + 3];

                    targetBytes[targetIndex] = (byte)CompositeChannelOverWhite(blue, alpha);
                    targetBytes[targetIndex + 1] = (byte)CompositeChannelOverWhite(green, alpha);
                    targetBytes[targetIndex + 2] = (byte)CompositeChannelOverWhite(red, alpha);
                }
            }

            Marshal.Copy(targetBytes, 0, bitmapData.Scan0, targetBytes.Length);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return bitmap;
    }

    private static Bitmap ConvertToMonochrome(Bitmap source)
    {
        var rectangle = new Rectangle(0, 0, source.Width, source.Height);
        var monochromeBitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format1bppIndexed);
        var palette = monochromeBitmap.Palette;
        palette.Entries[0] = Color.White;
        palette.Entries[1] = Color.Black;
        monochromeBitmap.Palette = palette;

        var sourceData = source.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var targetData = monochromeBitmap.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);

        try
        {
            var sourceStride = sourceData.Stride;
            var targetStride = targetData.Stride;
            var sourceBytes = new byte[Math.Abs(sourceStride) * source.Height];
            var targetBytes = new byte[Math.Abs(targetStride) * source.Height];

            Marshal.Copy(sourceData.Scan0, sourceBytes, 0, sourceBytes.Length);

            for (var y = 0; y < source.Height; y++)
            {
                var sourceRow = sourceStride > 0 ? y * sourceStride : (source.Height - 1 - y) * -sourceStride;
                var targetRow = targetStride > 0 ? y * targetStride : (source.Height - 1 - y) * -targetStride;
                var targetByteIndex = targetRow;
                var mask = 0x80;

                for (var x = 0; x < source.Width; x++)
                {
                    var sourceIndex = sourceRow + (x * 3);
                    var blue = sourceBytes[sourceIndex];
                    var green = sourceBytes[sourceIndex + 1];
                    var red = sourceBytes[sourceIndex + 2];
                    var luminance = ((red * 299) + (green * 587) + (blue * 114)) / 1000;

                    if (luminance < 180)
                    {
                        targetBytes[targetByteIndex] = (byte)(targetBytes[targetByteIndex] | mask);
                    }

                    mask >>= 1;
                    if (mask != 0)
                    {
                        continue;
                    }

                    mask = 0x80;
                    targetByteIndex++;
                }
            }

            Marshal.Copy(targetBytes, 0, targetData.Scan0, targetBytes.Length);
        }
        finally
        {
            source.UnlockBits(sourceData);
            monochromeBitmap.UnlockBits(targetData);
        }

        return monochromeBitmap;
    }

    private static int CompositeChannelOverWhite(byte color, byte alpha)
    {
        return ((color * alpha) + (255 * (255 - alpha))) / 255;
    }
}
