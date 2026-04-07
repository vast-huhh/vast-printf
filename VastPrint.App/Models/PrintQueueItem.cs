using System.IO;
using VastPrint.App.Infrastructure;

namespace VastPrint.App.Models;

public sealed class PrintQueueItem : ObservableObject
{
    private PrintJobStatus _status = PrintJobStatus.Pending;
    private string _note = "等待处理";
    private int? _pageCount;

    public PrintQueueItem(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Extension = Path.GetExtension(filePath).ToLowerInvariant();
    }

    public string FilePath { get; }

    public string FileName { get; }

    public string Extension { get; }

    public PrintJobStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string StatusText => Status switch
    {
        PrintJobStatus.Pending => "待处理",
        PrintJobStatus.Printing => "打印中",
        PrintJobStatus.Success => "成功",
        PrintJobStatus.Failed => "失败",
        PrintJobStatus.Cancelled => "已取消",
        _ => "未知",
    };

    public int? PageCount
    {
        get => _pageCount;
        set => SetProperty(ref _pageCount, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public void ResetForRetry()
    {
        Status = PrintJobStatus.Pending;
        Note = "等待处理";
        PageCount = null;
    }
}
