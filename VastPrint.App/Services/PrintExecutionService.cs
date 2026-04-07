using System.IO;
using VastPrint.App.Models;

namespace VastPrint.App.Services;

public sealed class PrintExecutionService
{
    private readonly IReadOnlyList<IFilePrintRenderer> _renderers;
    private readonly LogService _logService;

    public PrintExecutionService(IEnumerable<IFilePrintRenderer> renderers, LogService logService)
    {
        _renderers = renderers.ToList();
        _logService = logService;
        SupportedExtensions = _renderers
            .SelectMany(renderer => renderer.SupportedExtensions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension)
            .ToList();
    }

    public IReadOnlyList<string> SupportedExtensions { get; }

    public Task<PrintExecutionResult> PrintAsync(string filePath, AppSettings settings, CancellationToken cancellationToken)
    {
        var snapshot = settings.Clone();
        _logService.Info(
            $"PrintAsync start: file='{filePath}', printer='{snapshot.SelectedPrinterName ?? "<null>"}', " +
            $"landscape={snapshot.PageSetup.Landscape}, color={snapshot.PageSetup.ColorMode}, duplex={snapshot.PageSetup.DuplexMode}, copies={snapshot.PageSetup.Copies}");

        var completionSource = new TaskCompletionSource<PrintExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = PrintCore(filePath, snapshot, cancellationToken);
                _logService.Info(
                    $"PrintAsync completed: file='{filePath}', success={result.Success}, pages={result.PageCount?.ToString() ?? "<null>"}, message='{result.Message}'");
                completionSource.TrySetResult(result);
            }
            catch (OperationCanceledException)
            {
                _logService.Warning($"PrintAsync cancelled: file='{filePath}'");
                completionSource.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                _logService.Error($"PrintAsync exception: file='{filePath}', details={ex}");
                completionSource.TrySetResult(PrintExecutionResult.Failed(ex.Message));
            }
        })
        {
            IsBackground = true,
            Name = "VastPrintWorker",
        };

        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();

        return completionSource.Task;
    }

    private PrintExecutionResult PrintCore(string filePath, AppSettings settings, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var renderer = _renderers.FirstOrDefault(candidate => candidate.CanHandle(extension));
        _logService.Info(
            $"PrintCore renderer selection: file='{filePath}', extension='{extension}', renderer='{renderer?.GetType().Name ?? "<null>"}'");

        return renderer is null
            ? PrintExecutionResult.Failed($"当前项目暂不支持 {extension} 类型。")
            : renderer.Print(filePath, settings, cancellationToken);
    }
}
