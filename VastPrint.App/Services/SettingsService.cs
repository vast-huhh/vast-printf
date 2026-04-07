using System.IO;
using System.Linq;
using System.Text.Json;
using VastPrint.App.Models;

namespace VastPrint.App.Services;

public sealed class SettingsService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        _settingsPath = AppStoragePaths.SettingsFilePath;
    }

    public AppSettings Load(IEnumerable<string> supportedExtensions)
    {
        AppSettings? settings = null;
        var hasPersistedSettings = File.Exists(_settingsPath);
        var loadedFromFile = false;

        if (hasPersistedSettings)
        {
            try
            {
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), _serializerOptions);
                loadedFromFile = settings is not null;
            }
            catch
            {
                settings = null;
            }
        }

        settings ??= new AppSettings();
        Normalize(settings, supportedExtensions, loadedFromFile);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _serializerOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private static void Normalize(AppSettings settings, IEnumerable<string> supportedExtensions, bool keepEmptySelection)
    {
        var normalizedSupportedExtensions = supportedExtensions
            .Select(extension => extension.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension)
            .ToList();

        if (settings.PageSetup.Copies < 1)
        {
            settings.PageSetup.Copies = 1;
        }

        if (!Enum.IsDefined(settings.PageSetup.ColorMode))
        {
            settings.PageSetup.ColorMode = PrintColorMode.Default;
        }

        if (!Enum.IsDefined(settings.PageSetup.DuplexMode))
        {
            settings.PageSetup.DuplexMode = PrintDuplexMode.Default;
        }

        settings.PageSetup.ColorRenderDpi = Math.Clamp(settings.PageSetup.ColorRenderDpi, 120, 300);

        settings.EnabledExtensions = settings.EnabledExtensions
            .Select(extension => extension.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(normalizedSupportedExtensions.Contains)
            .ToList();

        if (settings.EnabledExtensions.Count == 0 && !keepEmptySelection)
        {
            settings.EnabledExtensions = normalizedSupportedExtensions;
        }
    }
}
