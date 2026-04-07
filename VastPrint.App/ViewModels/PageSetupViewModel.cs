using System.Collections.Generic;
using System.Windows.Input;
using VastPrint.App.Infrastructure;
using VastPrint.App.Models;

namespace VastPrint.App.ViewModels;

public sealed class PageSetupViewModel : ObservableObject
{
    private readonly Func<PageSetupProfile, string> _describePageSetup;
    private readonly Func<PageSetupProfile, PageSetupProfile?> _openSystemPageSetup;
    private readonly PageSetupProfile _nativeProfile;
    private readonly RelayCommand _decreaseColorRenderDpiCommand;
    private readonly RelayCommand _increaseColorRenderDpiCommand;
    private PrintColorMode _colorMode;
    private PrintDuplexMode _duplexMode;
    private int _colorRenderDpi;
    private string _nativePageSetupSummary;

    public PageSetupViewModel(
        string? printerName,
        PageSetupProfile currentProfile,
        bool supportsColor,
        bool canDuplex,
        Func<PageSetupProfile, string> describePageSetup,
        Func<PageSetupProfile, PageSetupProfile?> openSystemPageSetup)
    {
        _describePageSetup = describePageSetup;
        _openSystemPageSetup = openSystemPageSetup;
        _nativeProfile = currentProfile.Clone();
        _colorMode = currentProfile.ColorMode;
        _duplexMode = currentProfile.DuplexMode;
        _colorRenderDpi = currentProfile.ColorRenderDpi;
        _nativePageSetupSummary = _describePageSetup(_nativeProfile);

        PrinterName = string.IsNullOrWhiteSpace(printerName) ? "未选择打印机" : printerName;
        SupportsColor = supportsColor;
        CanDuplex = canDuplex;

        ColorModeOptions =
        [
            new KeyValuePair<PrintColorMode, string>(PrintColorMode.Default, "打印机默认"),
            new KeyValuePair<PrintColorMode, string>(PrintColorMode.Color, "彩色"),
            new KeyValuePair<PrintColorMode, string>(PrintColorMode.BlackAndWhite, "黑白"),
        ];

        DuplexModeOptions =
        [
            new KeyValuePair<PrintDuplexMode, string>(PrintDuplexMode.Default, "打印机默认"),
            new KeyValuePair<PrintDuplexMode, string>(PrintDuplexMode.Simplex, "单面"),
            new KeyValuePair<PrintDuplexMode, string>(PrintDuplexMode.DuplexLongEdge, "双面(长边翻页)"),
            new KeyValuePair<PrintDuplexMode, string>(PrintDuplexMode.DuplexShortEdge, "双面(短边翻页)"),
        ];

        OpenSystemPageSetupCommand = new RelayCommand(OpenSystemPageSetup, () => !string.IsNullOrWhiteSpace(printerName));
        _decreaseColorRenderDpiCommand = new RelayCommand(DecreaseColorRenderDpi, () => ColorRenderDpi > 120);
        _increaseColorRenderDpiCommand = new RelayCommand(IncreaseColorRenderDpi, () => ColorRenderDpi < 300);
    }

    public string PrinterName { get; }

    public bool SupportsColor { get; }

    public bool CanDuplex { get; }

    public string CapabilitySummary
    {
        get
        {
            var colorText = SupportsColor ? "支持彩色" : "不支持彩色";
            var duplexText = CanDuplex ? "支持双面" : "不支持双面";
            return $"当前打印机 {colorText}，{duplexText}。";
        }
    }

    public IReadOnlyList<KeyValuePair<PrintColorMode, string>> ColorModeOptions { get; }

    public IReadOnlyList<KeyValuePair<PrintDuplexMode, string>> DuplexModeOptions { get; }

    public ICommand OpenSystemPageSetupCommand { get; }

    public ICommand DecreaseColorRenderDpiCommand => _decreaseColorRenderDpiCommand;

    public ICommand IncreaseColorRenderDpiCommand => _increaseColorRenderDpiCommand;

    public PrintColorMode ColorMode
    {
        get => _colorMode;
        set => SetProperty(ref _colorMode, value);
    }

    public PrintDuplexMode DuplexMode
    {
        get => _duplexMode;
        set => SetProperty(ref _duplexMode, value);
    }

    public int ColorRenderDpi
    {
        get => _colorRenderDpi;
        set
        {
            var normalized = Math.Clamp(value, 120, 300);
            if (SetProperty(ref _colorRenderDpi, normalized))
            {
                OnPropertyChanged(nameof(ColorRenderDpiSummary));
                _decreaseColorRenderDpiCommand.RaiseCanExecuteChanged();
                _increaseColorRenderDpiCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ColorRenderDpiSummary => $"{ColorRenderDpi} DPI";

    public string NativePageSetupSummary
    {
        get => _nativePageSetupSummary;
        private set => SetProperty(ref _nativePageSetupSummary, value);
    }

    public PageSetupProfile BuildPageSetup()
    {
        var updatedProfile = _nativeProfile.Clone();
        updatedProfile.ColorMode = ColorMode;
        updatedProfile.DuplexMode = DuplexMode;
        updatedProfile.ColorRenderDpi = ColorRenderDpi;
        return updatedProfile;
    }

    private void OpenSystemPageSetup()
    {
        var updatedProfile = _openSystemPageSetup(BuildPageSetup());
        if (updatedProfile is null)
        {
            return;
        }

        _nativeProfile.Landscape = updatedProfile.Landscape;
        _nativeProfile.MarginLeft = updatedProfile.MarginLeft;
        _nativeProfile.MarginTop = updatedProfile.MarginTop;
        _nativeProfile.MarginRight = updatedProfile.MarginRight;
        _nativeProfile.MarginBottom = updatedProfile.MarginBottom;
        _nativeProfile.PaperName = updatedProfile.PaperName;
        _nativeProfile.PaperWidth = updatedProfile.PaperWidth;
        _nativeProfile.PaperHeight = updatedProfile.PaperHeight;
        _nativeProfile.Copies = updatedProfile.Copies;
        NativePageSetupSummary = _describePageSetup(BuildPageSetup());
    }

    private void DecreaseColorRenderDpi()
    {
        ColorRenderDpi -= 30;
    }

    private void IncreaseColorRenderDpi()
    {
        ColorRenderDpi += 30;
    }
}
