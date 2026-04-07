using System.IO;
using VastPrint.App.Models;

namespace VastPrint.App.Services;

public sealed class FileDiscoveryService
{
    public FileDiscoveryResult DiscoverFilesFromFolder(string folderPath, IEnumerable<string> enabledExtensions, bool includeSubDirectories)
    {
        var collectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var extensionSet = new HashSet<string>(
            enabledExtensions.Select(extension => extension.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(folderPath))
        {
            return new FileDiscoveryResult([], [$"目录不存在: {folderPath}"]);
        }

        var pendingFolders = new Stack<string>();
        pendingFolders.Push(folderPath);

        while (pendingFolders.Count > 0)
        {
            var currentFolder = pendingFolders.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(currentFolder))
                {
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extensionSet.Contains(extension))
                    {
                        collectedFiles.Add(file);
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"{currentFolder}: {ex.Message}");
            }

            if (!includeSubDirectories)
            {
                continue;
            }

            try
            {
                foreach (var subFolder in Directory.EnumerateDirectories(currentFolder))
                {
                    pendingFolders.Push(subFolder);
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"{currentFolder}: {ex.Message}");
            }
        }

        return new FileDiscoveryResult(
            collectedFiles.OrderBy(file => file).ToList(),
            warnings);
    }
}
