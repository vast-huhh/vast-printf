using VastPrint.App.Models;

namespace VastPrint.App.Services;

public interface IFilePrintRenderer
{
    IReadOnlyCollection<string> SupportedExtensions { get; }

    bool CanHandle(string extension);

    PrintExecutionResult Print(string filePath, AppSettings settings, CancellationToken cancellationToken);
}
