using System.Globalization;
using System.IO;
using System.Text;

namespace VastPrint.App.Services;

public sealed class LogService
{
    private readonly object _syncRoot = new();

    public string LogsDirectory => AppStoragePaths.LogsDirectory;

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Warning(string message)
    {
        Write("WARN", message);
    }

    public void Error(string message)
    {
        Write("ERROR", message);
    }

    private void Write(string level, string message)
    {
        var timestamp = DateTime.Now;
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"[{timestamp:yyyy-MM-dd HH:mm:ss}] [{level}] {message}");

        lock (_syncRoot)
        {
            File.AppendAllText(GetLogFilePath(timestamp), line + Environment.NewLine, Encoding.UTF8);
        }
    }

    private static string GetLogFilePath(DateTime timestamp)
    {
        return Path.Combine(AppStoragePaths.LogsDirectory, $"vastprint-{timestamp:yyyyMMdd}.log");
    }
}
