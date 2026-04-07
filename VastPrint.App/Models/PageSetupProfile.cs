namespace VastPrint.App.Models;

public sealed class PageSetupProfile
{
    public bool Landscape { get; set; }

    public int MarginLeft { get; set; } = 50;

    public int MarginTop { get; set; } = 50;

    public int MarginRight { get; set; } = 50;

    public int MarginBottom { get; set; } = 50;

    public string? PaperName { get; set; }

    public int PaperWidth { get; set; }

    public int PaperHeight { get; set; }

    public short Copies { get; set; } = 1;

    public PrintColorMode ColorMode { get; set; } = PrintColorMode.Default;

    public PrintDuplexMode DuplexMode { get; set; } = PrintDuplexMode.Default;

    public int ColorRenderDpi { get; set; } = 180;

    public PageSetupProfile Clone()
    {
        return new PageSetupProfile
        {
            Landscape = Landscape,
            MarginLeft = MarginLeft,
            MarginTop = MarginTop,
            MarginRight = MarginRight,
            MarginBottom = MarginBottom,
            PaperName = PaperName,
            PaperWidth = PaperWidth,
            PaperHeight = PaperHeight,
            Copies = Copies,
            ColorMode = ColorMode,
            DuplexMode = DuplexMode,
            ColorRenderDpi = ColorRenderDpi,
        };
    }
}
