using System.IO;

namespace VastPrint.App.Services;

public static class AppStoragePaths
{
    static AppStoragePaths()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VastPrint");

    public static string LogsDirectory { get; } = Path.Combine(RootDirectory, "logs");

    public static string SettingsFilePath { get; } = Path.Combine(RootDirectory, "appsettings.json");

    public static string HistoryFilePath { get; } = Path.Combine(RootDirectory, "print-history.json");
}
