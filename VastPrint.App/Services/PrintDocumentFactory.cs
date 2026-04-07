using System.Drawing.Printing;
using System.Linq;
using VastPrint.App.Models;

namespace VastPrint.App.Services;

public sealed class PrintDocumentFactory
{
    public string? ResolveInstalledPrinterName(string? preferredPrinterName, string? fallbackPrinterName = null)
    {
        var installedPrinters = PrinterSettings.InstalledPrinters
            .Cast<string>()
            .ToList();

        if (installedPrinters.Count == 0)
        {
            return null;
        }

        return FindPrinterName(installedPrinters, preferredPrinterName)
            ?? FindPrinterName(installedPrinters, fallbackPrinterName)
            ?? GetDefaultPrinterName()
            ?? installedPrinters[0];
    }

    public IReadOnlyList<string> GetInstalledPrinters()
    {
        var defaultPrinter = GetDefaultPrinterName();

        return PrinterSettings.InstalledPrinters
            .Cast<string>()
            .OrderBy(printer => printer.Equals(defaultPrinter, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(printer => printer)
            .ToList();
    }

    public string? GetDefaultPrinterName()
    {
        var printerSettings = new PrinterSettings();
        return printerSettings.IsValid ? printerSettings.PrinterName : PrinterSettings.InstalledPrinters.Cast<string>().FirstOrDefault();
    }

    public bool IsPrinterInstalled(string? printerName)
    {
        return FindPrinterName(
            PrinterSettings.InstalledPrinters.Cast<string>(),
            printerName) is not null;
    }

    public void EnsurePrinterSelection(AppSettings settings)
    {
        settings.SelectedPrinterName = ResolveInstalledPrinterName(settings.SelectedPrinterName);
    }

    public bool SupportsColor(string? printerName)
    {
        var printerSettings = CreatePrinterSettings(printerName);
        return printerSettings.IsValid && printerSettings.SupportsColor;
    }

    public bool CanDuplex(string? printerName)
    {
        var printerSettings = CreatePrinterSettings(printerName);
        return printerSettings.IsValid && printerSettings.CanDuplex;
    }

    public PrintDocument CreatePrintDocument(AppSettings settings, PageSetupProfile? overridePageSetup = null)
    {
        EnsurePrinterSelection(settings);

        var document = new PrintDocument();
        var resolvedPrinterName = ResolveInstalledPrinterName(settings.SelectedPrinterName);
        if (!string.IsNullOrWhiteSpace(resolvedPrinterName))
        {
            document.PrinterSettings.PrinterName = resolvedPrinterName;
        }

        var pageSetup = overridePageSetup ?? settings.PageSetup;
        document.PrinterSettings.Copies = Math.Max((short)1, pageSetup.Copies);
        ApplyPageSetup(document.DefaultPageSettings, document.PrinterSettings, pageSetup);
        return document;
    }

    public void ApplyPageSetup(PageSettings pageSettings, PrinterSettings printerSettings, PageSetupProfile profile)
    {
        pageSettings.Landscape = profile.Landscape;
        pageSettings.Margins = new Margins(
            profile.MarginLeft,
            profile.MarginRight,
            profile.MarginTop,
            profile.MarginBottom);

        if (profile.PaperWidth > 0 && profile.PaperHeight > 0)
        {
            var matchedPaper = printerSettings.PaperSizes
                .Cast<PaperSize>()
                .FirstOrDefault(paperSize =>
                    paperSize.Width == profile.PaperWidth &&
                    paperSize.Height == profile.PaperHeight);

            pageSettings.PaperSize = matchedPaper
                ?? new PaperSize(profile.PaperName ?? "Custom", profile.PaperWidth, profile.PaperHeight);
        }

        if (profile.ColorMode != PrintColorMode.Default)
        {
            pageSettings.Color = profile.ColorMode == PrintColorMode.Color;
        }

        if (profile.DuplexMode != PrintDuplexMode.Default && printerSettings.CanDuplex)
        {
            printerSettings.Duplex = profile.DuplexMode switch
            {
                PrintDuplexMode.Simplex => Duplex.Simplex,
                PrintDuplexMode.DuplexLongEdge => Duplex.Vertical,
                PrintDuplexMode.DuplexShortEdge => Duplex.Horizontal,
                _ => printerSettings.Duplex,
            };
        }
    }

    public PageSetupProfile Capture(PageSettings pageSettings, PrinterSettings printerSettings, PageSetupProfile? previousProfile = null)
    {
        return new PageSetupProfile
        {
            Landscape = pageSettings.Landscape,
            MarginLeft = pageSettings.Margins.Left,
            MarginTop = pageSettings.Margins.Top,
            MarginRight = pageSettings.Margins.Right,
            MarginBottom = pageSettings.Margins.Bottom,
            PaperName = pageSettings.PaperSize?.PaperName,
            PaperWidth = pageSettings.PaperSize?.Width ?? 0,
            PaperHeight = pageSettings.PaperSize?.Height ?? 0,
            Copies = Math.Max((short)1, printerSettings.Copies),
            ColorMode = previousProfile?.ColorMode ?? PrintColorMode.Default,
            DuplexMode = previousProfile?.DuplexMode ?? PrintDuplexMode.Default,
        };
    }

    public string Describe(PageSetupProfile pageSetup)
    {
        var orientationText = pageSetup.Landscape ? "横向" : "纵向";
        var colorText = pageSetup.ColorMode switch
        {
            PrintColorMode.Color => "彩色",
            PrintColorMode.BlackAndWhite => "黑白",
            _ => "打印机默认",
        };
        var duplexText = pageSetup.DuplexMode switch
        {
            PrintDuplexMode.Simplex => "单面",
            PrintDuplexMode.DuplexLongEdge => "双面(长边翻页)",
            PrintDuplexMode.DuplexShortEdge => "双面(短边翻页)",
            _ => "打印机默认",
        };
        var paperText = string.IsNullOrWhiteSpace(pageSetup.PaperName)
            ? "系统默认纸张"
            : $"{pageSetup.PaperName} ({ToMillimeter(pageSetup.PaperWidth):0.#} x {ToMillimeter(pageSetup.PaperHeight):0.#} mm)";

        return $"{paperText}，{orientationText}，边距 {ToMillimeter(pageSetup.MarginLeft):0.#}/{ToMillimeter(pageSetup.MarginTop):0.#}/{ToMillimeter(pageSetup.MarginRight):0.#}/{ToMillimeter(pageSetup.MarginBottom):0.#} mm，颜色 {colorText}，双面 {duplexText}，份数 {Math.Max((short)1, pageSetup.Copies)}";
    }

    private static double ToMillimeter(int hundredthsOfAnInch)
    {
        return hundredthsOfAnInch * 0.254;
    }

    private PrinterSettings CreatePrinterSettings(string? printerName)
    {
        var printerSettings = new PrinterSettings();
        var resolvedPrinterName = ResolveInstalledPrinterName(printerName);
        if (!string.IsNullOrWhiteSpace(resolvedPrinterName))
        {
            printerSettings.PrinterName = resolvedPrinterName;
        }

        return printerSettings;
    }

    private static string? FindPrinterName(IEnumerable<string> installedPrinters, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        return installedPrinters.FirstOrDefault(printerName =>
            printerName.Equals(candidate, StringComparison.OrdinalIgnoreCase));
    }
}
