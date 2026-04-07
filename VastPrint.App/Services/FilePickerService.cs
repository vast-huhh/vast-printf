using Forms = System.Windows.Forms;

namespace VastPrint.App.Services;

public sealed class FilePickerService
{
    public IReadOnlyList<string> PickFiles(IEnumerable<string> supportedExtensions)
    {
        var extensions = supportedExtensions.OrderBy(extension => extension).ToList();
        var filterPattern = string.Join(";", extensions.Select(extension => $"*{extension}"));
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择要打印的文件",
            Multiselect = true,
            Filter = extensions.Count == 0
                ? "所有文件|*.*"
                : $"支持的文件|{filterPattern}|所有文件|*.*",
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public string? PickFolder()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择待扫描的文件夹",
            ShowNewFolderButton = false,
        };

        return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }
}
