using System.IO;
using System.Text.Json;
using VastPrint.App.Models;

namespace VastPrint.App.Services;

public sealed class PrintHistoryService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    public IReadOnlyList<PrintHistoryEntry> Load()
    {
        if (!File.Exists(AppStoragePaths.HistoryFilePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PrintHistoryEntry>>(
                File.ReadAllText(AppStoragePaths.HistoryFilePath),
                _serializerOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Append(PrintHistoryEntry entry, int maxEntries = 300)
    {
        var history = Load().ToList();
        history.Insert(0, entry);

        if (history.Count > maxEntries)
        {
            history = history.Take(maxEntries).ToList();
        }

        File.WriteAllText(
            AppStoragePaths.HistoryFilePath,
            JsonSerializer.Serialize(history, _serializerOptions));
    }
}
