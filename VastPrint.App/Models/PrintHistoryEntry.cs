namespace VastPrint.App.Models;

public sealed class PrintHistoryEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public string FileName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public string PrinterName { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;

    public int? PageCount { get; init; }

    public string Message { get; init; } = string.Empty;

    public string TimestampText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
}
