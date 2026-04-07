using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using VastPrint.App.Infrastructure;
using VastPrint.App.Models;
using VastPrint.App.Services;

namespace VastPrint.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly FilePickerService _filePickerService;
    private readonly FileDiscoveryService _fileDiscoveryService;
    private readonly SettingsDialogService _settingsDialogService;
    private readonly PrinterDialogService _printerDialogService;
    private readonly PrintExecutionService _printExecutionService;
    private readonly PrintHistoryService _printHistoryService;
    private readonly LogService _logService;
    private readonly ManualResetEventSlim _pauseGate = new(true);

    private readonly RelayCommand _addFilesCommand;
    private readonly RelayCommand _addFolderCommand;
    private readonly RelayCommand _openSettingsCommand;
    private readonly RelayCommand _openPrinterPropertiesCommand;
    private readonly RelayCommand _openPageSetupCommand;
    private readonly RelayCommand _pausePrintCommand;
    private readonly RelayCommand _resumePrintCommand;
    private readonly RelayCommand _cancelPrintCommand;
    private readonly RelayCommand _removeSelectedItemCommand;
    private readonly RelayCommand _clearQueueCommand;
    private readonly RelayCommand _retryFailedCommand;
    private readonly RelayCommand _moveUpCommand;
    private readonly RelayCommand _moveDownCommand;
    private readonly AsyncRelayCommand _startPrintCommand;
    private readonly AsyncRelayCommand _startSelectedPrintCommand;

    private AppSettings _settings;
    private CancellationTokenSource? _printCancellationSource;
    private string? _selectedPrinter;
    private PrintQueueItem? _selectedQueueItem;
    private readonly List<PrintQueueItem> _selectedQueueItems = [];
    private string _pageSetupSummary = string.Empty;
    private string _enabledTypesSummary = string.Empty;
    private string _statusMessage = string.Empty;
    private string _queueSummary = "队列为空";
    private string _historySummary = "暂无打印历史";
    private string _storageSummary = string.Empty;
    private bool _isPrinting;
    private bool _isPaused;
    private bool _isRefreshingPrinterSelection;

    public MainViewModel(
        SettingsService settingsService,
        FilePickerService filePickerService,
        FileDiscoveryService fileDiscoveryService,
        SettingsDialogService settingsDialogService,
        PrinterDialogService printerDialogService,
        PrintExecutionService printExecutionService,
        PrintHistoryService printHistoryService,
        LogService logService)
    {
        _settingsService = settingsService;
        _filePickerService = filePickerService;
        _fileDiscoveryService = fileDiscoveryService;
        _settingsDialogService = settingsDialogService;
        _printerDialogService = printerDialogService;
        _printExecutionService = printExecutionService;
        _printHistoryService = printHistoryService;
        _logService = logService;

        _settings = _settingsService.Load(_printExecutionService.SupportedExtensions);

        Printers = [];
        QueueItems = [];
        PrintHistory = [];

        QueueItems.CollectionChanged += OnQueueItemsChanged;

        _addFilesCommand = new RelayCommand(AddFiles, CanEditQueue);
        _addFolderCommand = new RelayCommand(AddFolder, CanEditQueue);
        _openSettingsCommand = new RelayCommand(OpenSettings, CanEditQueue);
        _openPrinterPropertiesCommand = new RelayCommand(OpenPrinterProperties, CanEditQueue);
        _openPageSetupCommand = new RelayCommand(OpenPageSetup, CanEditQueue);
        _pausePrintCommand = new RelayCommand(PausePrint, () => IsPrinting && !IsPaused);
        _resumePrintCommand = new RelayCommand(ResumePrint, () => IsPrinting && IsPaused);
        _cancelPrintCommand = new RelayCommand(CancelPrint, () => IsPrinting);
        _removeSelectedItemCommand = new RelayCommand(RemoveSelectedItem, () => !IsPrinting && _selectedQueueItems.Count > 0);
        _clearQueueCommand = new RelayCommand(ClearQueue, () => !IsPrinting && QueueItems.Count > 0);
        _retryFailedCommand = new RelayCommand(RetryFailedItems, () => !IsPrinting && QueueItems.Any(item => item.Status is PrintJobStatus.Failed or PrintJobStatus.Cancelled));
        _moveUpCommand = new RelayCommand(MoveSelectedUp, () => !IsPrinting && SelectedQueueItem is not null && QueueItems.IndexOf(SelectedQueueItem) > 0);
        _moveDownCommand = new RelayCommand(MoveSelectedDown, () => !IsPrinting && SelectedQueueItem is not null && QueueItems.IndexOf(SelectedQueueItem) < QueueItems.Count - 1);
        _startPrintCommand = new AsyncRelayCommand(StartPrintAsync, () => !IsPrinting && QueueItems.Count > 0);
        _startSelectedPrintCommand = new AsyncRelayCommand(StartSelectedPrintAsync, () => !IsPrinting && _selectedQueueItems.Count > 0);

        AddFilesCommand = _addFilesCommand;
        AddFolderCommand = _addFolderCommand;
        OpenSettingsCommand = _openSettingsCommand;
        OpenPrinterPropertiesCommand = _openPrinterPropertiesCommand;
        OpenPageSetupCommand = _openPageSetupCommand;
        PausePrintCommand = _pausePrintCommand;
        ResumePrintCommand = _resumePrintCommand;
        CancelPrintCommand = _cancelPrintCommand;
        RemoveSelectedItemCommand = _removeSelectedItemCommand;
        ClearQueueCommand = _clearQueueCommand;
        RetryFailedCommand = _retryFailedCommand;
        MoveUpCommand = _moveUpCommand;
        MoveDownCommand = _moveDownCommand;
        StartPrintCommand = _startPrintCommand;
        StartSelectedPrintCommand = _startSelectedPrintCommand;

        LoadInitialState();
    }

    public ObservableCollection<string> Printers { get; }

    public ObservableCollection<PrintQueueItem> QueueItems { get; }

    public ObservableCollection<PrintHistoryEntry> PrintHistory { get; }

    public ICommand AddFilesCommand { get; }

    public ICommand AddFolderCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    public ICommand OpenPrinterPropertiesCommand { get; }

    public ICommand OpenPageSetupCommand { get; }

    public ICommand StartPrintCommand { get; }

    public ICommand StartSelectedPrintCommand { get; }

    public ICommand PausePrintCommand { get; }

    public ICommand ResumePrintCommand { get; }

    public ICommand CancelPrintCommand { get; }

    public ICommand RemoveSelectedItemCommand { get; }

    public ICommand ClearQueueCommand { get; }

    public ICommand RetryFailedCommand { get; }

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }

    public string? SelectedPrinter
    {
        get => _selectedPrinter;
        set
        {
            if (!SetProperty(ref _selectedPrinter, value))
            {
                return;
            }

            _logService.Info($"SelectedPrinter changed: '{value ?? "<null>"}' (refreshing={_isRefreshingPrinterSelection})");
            if (_isRefreshingPrinterSelection)
            {
                return;
            }

            _settings.SelectedPrinterName = value;
            PersistSettings();
            RefreshPageSetupSummary();
        }
    }

    public PrintQueueItem? SelectedQueueItem
    {
        get => _selectedQueueItem;
        set
        {
            if (SetProperty(ref _selectedQueueItem, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public void UpdateSelectedQueueItems(IEnumerable<PrintQueueItem> items)
    {
        _selectedQueueItems.Clear();
        _selectedQueueItems.AddRange(items.Distinct());
        RefreshCommandStates();
    }

    public string PageSetupSummary
    {
        get => _pageSetupSummary;
        private set => SetProperty(ref _pageSetupSummary, value);
    }

    public string EnabledTypesSummary
    {
        get => _enabledTypesSummary;
        private set => SetProperty(ref _enabledTypesSummary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string QueueSummary
    {
        get => _queueSummary;
        private set => SetProperty(ref _queueSummary, value);
    }

    public string HistorySummary
    {
        get => _historySummary;
        private set => SetProperty(ref _historySummary, value);
    }

    public string StorageSummary
    {
        get => _storageSummary;
        private set => SetProperty(ref _storageSummary, value);
    }

    public bool IsPrinting
    {
        get => _isPrinting;
        private set
        {
            if (SetProperty(ref _isPrinting, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (SetProperty(ref _isPaused, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public void HandleDroppedPaths(IEnumerable<string> paths)
    {
        if (IsPrinting)
        {
            StatusMessage = "正在打印时暂不接受拖拽导入。";
            return;
        }

        var directFiles = new List<string>();
        var folderFiles = new List<string>();
        var unsupportedCount = 0;
        var warningCount = 0;

        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(path))
            {
                if (IsSupportedFile(path))
                {
                    directFiles.Add(path);
                }
                else
                {
                    unsupportedCount++;
                }

                continue;
            }

            if (!Directory.Exists(path))
            {
                unsupportedCount++;
                continue;
            }

            var discoveryResult = _fileDiscoveryService.DiscoverFilesFromFolder(
                path,
                _settings.EnabledExtensions,
                _settings.IncludeSubDirectories);

            folderFiles.AddRange(discoveryResult.Files);
            warningCount += discoveryResult.Warnings.Count;

            foreach (var warning in discoveryResult.Warnings)
            {
                _logService.Warning($"扫描目录警告: {warning}");
            }
        }

        var addedCount = EnqueueFiles(directFiles.Concat(folderFiles));
        StatusMessage = $"拖拽导入完成，新增 {addedCount} 个文件，忽略不支持项 {unsupportedCount} 个，扫描警告 {warningCount} 条。";
    }

    private void LoadInitialState()
    {
        RefreshPrintersAndSelection();

        foreach (var historyItem in _printHistoryService.Load())
        {
            PrintHistory.Add(historyItem);
        }

        RefreshEnabledTypesSummary();
        RefreshPageSetupSummary();
        RefreshQueueSummary();
        RefreshHistorySummary();
        StorageSummary = $"历史、日志与设置保存在 {AppStoragePaths.RootDirectory}";
        PersistSettings();

        StatusMessage = Printers.Count == 0
            ? "未发现可用打印机，请先检查系统打印机配置。"
            : "系统已就绪，可以开始导入文件或文件夹。";
    }

    private bool CanEditQueue()
    {
        return !IsPrinting;
    }

    private void AddFiles()
    {
        var files = _filePickerService.PickFiles(_printExecutionService.SupportedExtensions);
        if (files.Count == 0)
        {
            return;
        }

        var addedCount = EnqueueFiles(files);
        StatusMessage = addedCount == 0
            ? "所选文件都已经在队列中。"
            : $"已添加 {addedCount} 个文件到队列。";
    }

    private void AddFolder()
    {
        var folderPath = _filePickerService.PickFolder();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        var discoveryResult = _fileDiscoveryService.DiscoverFilesFromFolder(
            folderPath,
            _settings.EnabledExtensions,
            _settings.IncludeSubDirectories);

        foreach (var warning in discoveryResult.Warnings)
        {
            _logService.Warning($"扫描目录警告: {warning}");
        }

        var addedCount = EnqueueFiles(discoveryResult.Files);
        var warningText = discoveryResult.Warnings.Count > 0
            ? $" 扫描时有 {discoveryResult.Warnings.Count} 条警告。"
            : string.Empty;

        StatusMessage = $"已从文件夹导入 {addedCount} 个文件。{warningText}";
    }

    private void OpenSettings()
    {
        var updatedSettings = _settingsDialogService.Show(_settings, _printExecutionService.SupportedExtensions);
        if (updatedSettings is null)
        {
            return;
        }

        _settings.IncludeSubDirectories = updatedSettings.IncludeSubDirectories;
        _settings.ContinueOnError = updatedSettings.ContinueOnError;
        _settings.EnabledExtensions = updatedSettings.EnabledExtensions;

        PersistSettings();
        RefreshEnabledTypesSummary();
        StatusMessage = "设置已保存，后续导入会使用新的筛选规则。";
        _logService.Info("用户更新了导入与容错设置。");
    }

    private void OpenPrinterProperties()
    {
        _logService.Info($"OpenPrinterProperties before dialog: settings='{_settings.SelectedPrinterName ?? "<null>"}', view='{_selectedPrinter ?? "<null>"}'");
        if (!_printerDialogService.ShowPrinterProperties(_settings))
        {
            _logService.Info("OpenPrinterProperties cancelled by user.");
            return;
        }

        _logService.Info($"OpenPrinterProperties after dialog: settings='{_settings.SelectedPrinterName ?? "<null>"}', view='{_selectedPrinter ?? "<null>"}'");
        RefreshPrintersAndSelection();
        RefreshPageSetupSummary();
        PersistSettings();
        StatusMessage = "打印机属性已更新。";
        _logService.Info($"更新打印机属性，当前打印机: {_settings.SelectedPrinterName}");
    }

    private void OpenPageSetup()
    {
        _logService.Info($"OpenPageSetup before dialog: settings='{_settings.SelectedPrinterName ?? "<null>"}', view='{_selectedPrinter ?? "<null>"}'");
        if (!_printerDialogService.ShowPageSetup(_settings))
        {
            _logService.Info("OpenPageSetup cancelled by user.");
            return;
        }

        _logService.Info($"OpenPageSetup after dialog: settings='{_settings.SelectedPrinterName ?? "<null>"}', view='{_selectedPrinter ?? "<null>"}'");
        RefreshPrintersAndSelection();
        RefreshPageSetupSummary();
        PersistSettings();
        StatusMessage = "页面设置已更新。";
        _logService.Info("页面设置已更新。");
    }

    private async Task StartPrintAsync()
    {
        if (QueueItems.Count == 0)
        {
            StatusMessage = "队列为空，请先导入文件。";
            return;
        }

        _printerDialogService.EnsurePrinterSelection(_settings);
        if (string.IsNullOrWhiteSpace(_settings.SelectedPrinterName))
        {
            StatusMessage = "当前没有可用打印机，无法开始打印。";
            return;
        }

        var pendingItems = QueueItems
            .Where(item => item.Status is not PrintJobStatus.Success)
            .ToList();

        if (pendingItems.Count == 0)
        {
            StatusMessage = "队列中的文件都已经打印成功。";
            return;
        }

        foreach (var item in pendingItems.Where(item => item.Status is PrintJobStatus.Failed or PrintJobStatus.Cancelled))
        {
            item.ResetForRetry();
        }

        await ExecutePrintLoopAsync(
            pendingItems,
            $"开始打印，共 {pendingItems.Count} 个文件。",
            cancelWholeQueueOnCancel: true);
    }

    private async Task StartSelectedPrintAsync()
    {
        var selectedItems = _selectedQueueItems
            .Distinct()
            .ToList();

        if (selectedItems.Count == 0)
        {
            StatusMessage = "请先在队列中选中要打印的文件。";
            return;
        }

        _printerDialogService.EnsurePrinterSelection(_settings);
        if (string.IsNullOrWhiteSpace(_settings.SelectedPrinterName))
        {
            StatusMessage = "当前没有可用打印机，无法开始打印。";
            return;
        }

        foreach (var item in selectedItems)
        {
            item.ResetForRetry();
        }

        await ExecutePrintLoopAsync(
            selectedItems,
            selectedItems.Count == 1
                ? $"开始打印选中项：{selectedItems[0].FileName}"
                : $"开始打印选中项，共 {selectedItems.Count} 个文件。",
            cancelWholeQueueOnCancel: false);
    }

    private async Task ExecutePrintLoopAsync(
        IReadOnlyList<PrintQueueItem> items,
        string startMessage,
        bool cancelWholeQueueOnCancel)
    {
        _printCancellationSource = new CancellationTokenSource();
        _pauseGate.Set();
        IsPaused = false;
        IsPrinting = true;
        StatusMessage = startMessage;
        _logService.Info($"开始打印任务，打印机: {_settings.SelectedPrinterName}，待处理文件数: {items.Count}");

        try
        {
            foreach (var item in items)
            {
                await WaitForPauseGateAsync(_printCancellationSource.Token);

                if (!File.Exists(item.FilePath))
                {
                    item.Status = PrintJobStatus.Failed;
                    item.Note = "文件不存在或路径已失效";
                    RecordHistory(item);
                    _logService.Error($"文件不存在: {item.FilePath}");
                    RefreshQueueSummary();

                    if (!_settings.ContinueOnError)
                    {
                        StatusMessage = "检测到缺失文件，任务已停止。";
                        break;
                    }

                    continue;
                }

                item.Status = PrintJobStatus.Printing;
                item.Note = "正在渲染并发送到打印机";
                RefreshQueueSummary();

                PrintExecutionResult result;
                try
                {
                    result = await _printExecutionService.PrintAsync(item.FilePath, _settings, _printCancellationSource.Token);
                }
                catch (OperationCanceledException)
                {
                    item.Status = PrintJobStatus.Cancelled;
                    item.Note = "用户取消了当前任务";
                    RecordHistory(item);
                    _logService.Warning($"用户取消打印: {item.FilePath}");
                    RefreshQueueSummary();
                    throw;
                }

                item.PageCount = result.PageCount;
                item.Status = result.Success ? PrintJobStatus.Success : PrintJobStatus.Failed;
                item.Note = result.Message;
                RecordHistory(item);
                RefreshQueueSummary();

                if (result.Success)
                {
                    _logService.Info($"打印成功: {item.FilePath}，页数: {item.PageCount}");
                }
                else
                {
                    _logService.Error($"打印失败: {item.FilePath}，原因: {result.Message}");
                }

                if (!result.Success && !_settings.ContinueOnError)
                {
                    StatusMessage = $"文件 {item.FileName} 打印失败，队列已停止。";
                    break;
                }
            }

            if (items.All(item => item.Status is PrintJobStatus.Success))
            {
                StatusMessage = items.Count == 1 ? $"已完成打印：{items[0].FileName}" : "全部文件打印完成。";
            }
            else if (items.Any(item => item.Status == PrintJobStatus.Failed))
            {
                StatusMessage = items.Count == 1 ? $"打印失败：{items[0].FileName}" : "打印任务结束，部分文件失败。";
            }
            else if (items.Any(item => item.Status == PrintJobStatus.Cancelled))
            {
                StatusMessage = cancelWholeQueueOnCancel ? "整队任务已取消。" : "打印任务已取消。";
            }
        }
        catch (OperationCanceledException)
        {
            if (cancelWholeQueueOnCancel)
            {
                CancelRemainingPendingItems("用户取消了整队任务");
                StatusMessage = "整队任务已取消。";
            }
            else
            {
                StatusMessage = "打印任务已取消。";
            }
        }
        finally
        {
            _printCancellationSource?.Dispose();
            _printCancellationSource = null;
            IsPaused = false;
            IsPrinting = false;
            _pauseGate.Set();
            RefreshQueueSummary();
            RefreshHistorySummary();
        }
    }

    private void PausePrint()
    {
        if (!IsPrinting || IsPaused)
        {
            return;
        }

        _pauseGate.Reset();
        IsPaused = true;
        StatusMessage = "队列将在当前文件完成后暂停。";
        _logService.Info("打印队列已请求暂停。");
    }

    private void ResumePrint()
    {
        if (!IsPrinting || !IsPaused)
        {
            return;
        }

        _pauseGate.Set();
        IsPaused = false;
        StatusMessage = "队列已继续执行。";
        _logService.Info("打印队列已继续。");
    }

    private async Task WaitForPauseGateAsync(CancellationToken cancellationToken)
    {
        if (_pauseGate.IsSet)
        {
            return;
        }

        StatusMessage = "队列已暂停，点击继续打印。";
        _logService.Info("打印队列已暂停，等待继续。");
        await Task.Run(() => _pauseGate.Wait(cancellationToken), cancellationToken);
    }

    private void CancelPrint()
    {
        if (!IsPrinting)
        {
            return;
        }

        _pauseGate.Set();
        _printCancellationSource?.Cancel();
        StatusMessage = "正在尝试取消整队任务...";
    }

    private void CancelRemainingPendingItems(string note)
    {
        var cancelledCount = 0;
        foreach (var item in QueueItems.Where(item => item.Status == PrintJobStatus.Pending))
        {
            item.Status = PrintJobStatus.Cancelled;
            item.Note = note;
            RecordHistory(item);
            cancelledCount++;
        }

        if (cancelledCount > 0)
        {
            _logService.Warning($"整队取消完成，额外标记 {cancelledCount} 个待处理文件为已取消。");
        }

        RefreshQueueSummary();
    }

    private void RemoveSelectedItem()
    {
        var itemsToRemove = _selectedQueueItems
            .Distinct()
            .ToList();

        if (itemsToRemove.Count == 0)
        {
            return;
        }

        foreach (var item in itemsToRemove)
        {
            QueueItems.Remove(item);
        }

        _selectedQueueItems.Clear();
        SelectedQueueItem = null;
        StatusMessage = itemsToRemove.Count == 1
            ? "已移除选中的队列项。"
            : $"已移除 {itemsToRemove.Count} 个选中的队列项。";
    }

    private void ClearQueue()
    {
        QueueItems.Clear();
        _selectedQueueItems.Clear();
        SelectedQueueItem = null;
        StatusMessage = "队列已清空。";
    }

    private void RetryFailedItems()
    {
        var retryCount = 0;
        foreach (var item in QueueItems.Where(item => item.Status is PrintJobStatus.Failed or PrintJobStatus.Cancelled))
        {
            item.ResetForRetry();
            retryCount++;
        }

        StatusMessage = retryCount == 0
            ? "当前没有可重试的失败项。"
            : $"已将 {retryCount} 个失败或取消项重置为待处理。";

        RefreshQueueSummary();
        RefreshCommandStates();
    }

    private void MoveSelectedUp()
    {
        if (SelectedQueueItem is null)
        {
            return;
        }

        var currentIndex = QueueItems.IndexOf(SelectedQueueItem);
        if (currentIndex <= 0)
        {
            return;
        }

        QueueItems.Move(currentIndex, currentIndex - 1);
        StatusMessage = "已上移选中的队列项。";
        RefreshCommandStates();
    }

    private void MoveSelectedDown()
    {
        if (SelectedQueueItem is null)
        {
            return;
        }

        var currentIndex = QueueItems.IndexOf(SelectedQueueItem);
        if (currentIndex < 0 || currentIndex >= QueueItems.Count - 1)
        {
            return;
        }

        QueueItems.Move(currentIndex, currentIndex + 1);
        StatusMessage = "已下移选中的队列项。";
        RefreshCommandStates();
    }

    private int EnqueueFiles(IEnumerable<string> files)
    {
        var existingFiles = new HashSet<string>(
            QueueItems.Select(item => item.FilePath),
            StringComparer.OrdinalIgnoreCase);

        var addedCount = 0;
        foreach (var file in files.Where(IsSupportedFile))
        {
            if (!existingFiles.Add(file))
            {
                continue;
            }

            QueueItems.Add(new PrintQueueItem(file));
            addedCount++;
        }

        return addedCount;
    }

    private bool IsSupportedFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return _printExecutionService.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private void RecordHistory(PrintQueueItem item)
    {
        var historyEntry = new PrintHistoryEntry
        {
            Timestamp = DateTime.Now,
            FileName = item.FileName,
            FilePath = item.FilePath,
            PrinterName = _settings.SelectedPrinterName ?? "未选择打印机",
            StatusText = item.StatusText,
            PageCount = item.PageCount,
            Message = item.Note,
        };

        PrintHistory.Insert(0, historyEntry);
        while (PrintHistory.Count > 300)
        {
            PrintHistory.RemoveAt(PrintHistory.Count - 1);
        }

        _printHistoryService.Append(historyEntry);
        RefreshHistorySummary();
    }

    private void RefreshPrinters()
    {
        Printers.Clear();
        foreach (var printer in _printerDialogService.GetInstalledPrinters())
        {
            Printers.Add(printer);
        }
    }

    private void RefreshPrintersAndSelection()
    {
        var preferredPrinter = _settings.SelectedPrinterName ?? _selectedPrinter;
        _logService.Info($"RefreshPrintersAndSelection start: settings='{_settings.SelectedPrinterName ?? "<null>"}', view='{_selectedPrinter ?? "<null>"}'");

        _isRefreshingPrinterSelection = true;
        try
        {
            _settings.SelectedPrinterName = preferredPrinter;
            _printerDialogService.EnsurePrinterSelection(_settings);
            RefreshPrinters();

            if (_settings.SelectedPrinterName is not null && !Printers.Contains(_settings.SelectedPrinterName))
            {
                Printers.Insert(0, _settings.SelectedPrinterName);
            }

            SyncSelectedPrinter();
        }
        finally
        {
            _isRefreshingPrinterSelection = false;
        }

        _logService.Info($"RefreshPrintersAndSelection end: settings='{_settings.SelectedPrinterName ?? "<null>"}', view='{_selectedPrinter ?? "<null>"}', count={Printers.Count}");
    }

    private void SyncSelectedPrinter()
    {
        _selectedPrinter = _settings.SelectedPrinterName;
        OnPropertyChanged(nameof(SelectedPrinter));
    }

    private void RefreshEnabledTypesSummary()
    {
        EnabledTypesSummary = _settings.EnabledExtensions.Count == 0
            ? "当前未启用任何类型，文件夹导入时不会加入文件。"
            : string.Join("  ", _settings.EnabledExtensions.OrderBy(extension => extension));
    }

    private void RefreshPageSetupSummary()
    {
        PageSetupSummary = _printerDialogService.DescribePageSetup(_settings.PageSetup);
    }

    private void RefreshQueueSummary()
    {
        if (QueueItems.Count == 0)
        {
            QueueSummary = "队列为空";
            return;
        }

        var successCount = QueueItems.Count(item => item.Status == PrintJobStatus.Success);
        var failedCount = QueueItems.Count(item => item.Status == PrintJobStatus.Failed);
        var printingCount = QueueItems.Count(item => item.Status == PrintJobStatus.Printing);
        var pendingCount = QueueItems.Count(item => item.Status == PrintJobStatus.Pending);
        var cancelledCount = QueueItems.Count(item => item.Status == PrintJobStatus.Cancelled);

        QueueSummary = $"共 {QueueItems.Count} 个，成功 {successCount}，失败 {failedCount}，打印中 {printingCount}，待处理 {pendingCount}，取消 {cancelledCount}";
    }

    private void RefreshHistorySummary()
    {
        HistorySummary = PrintHistory.Count == 0
            ? "暂无打印历史"
            : $"最近记录 {PrintHistory.Count} 条";
    }

    private void PersistSettings()
    {
        _settingsService.Save(_settings);
    }

    private void OnQueueItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshQueueSummary();
        RefreshCommandStates();
    }

    private void RefreshCommandStates()
    {
        _addFilesCommand.RaiseCanExecuteChanged();
        _addFolderCommand.RaiseCanExecuteChanged();
        _openSettingsCommand.RaiseCanExecuteChanged();
        _openPrinterPropertiesCommand.RaiseCanExecuteChanged();
        _openPageSetupCommand.RaiseCanExecuteChanged();
        _pausePrintCommand.RaiseCanExecuteChanged();
        _resumePrintCommand.RaiseCanExecuteChanged();
        _cancelPrintCommand.RaiseCanExecuteChanged();
        _removeSelectedItemCommand.RaiseCanExecuteChanged();
        _clearQueueCommand.RaiseCanExecuteChanged();
        _retryFailedCommand.RaiseCanExecuteChanged();
        _moveUpCommand.RaiseCanExecuteChanged();
        _moveDownCommand.RaiseCanExecuteChanged();
        _startPrintCommand.RaiseCanExecuteChanged();
        _startSelectedPrintCommand.RaiseCanExecuteChanged();
    }
}
