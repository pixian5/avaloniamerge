using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Media;
using AvaloniaMerge.Core;

namespace AvaloniaMerge.Gui.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private enum ToastKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    private string _leftPath = string.Empty;
    private string _rightPath = string.Empty;
    private bool _ignoreCase;
    private string _statusMessage = string.Empty;
    private string _summary = string.Empty;
    private int _selectedTab;
    private bool _isBusy;
    private string _directoryFilter = "不同";
    private string _directorySearchText = string.Empty;
    private string _directorySort = "相对路径";
    private bool _directorySortDescending;
    private string _directoryCountText = "总数 0 | 相同 0 | 不同 0";
    private readonly List<DirectoryItemViewModel> _allDirectoryItems = new();
    private CompareMethodOption _selectedDirectoryCompareMethod;
    private CancellationTokenSource? _toastCancellationTokenSource;
    private ToastKind _toastKind = ToastKind.Info;

    public MainViewModel()
    {
        DirectoryFilterOptions = new List<string> { "不同", "全部", "相同", "新增", "删除", "修改", "类型" };
        DirectorySortOptions = new List<string> { "相对路径", "状态", "左大小", "右大小", "左修改时间", "右修改时间" };
        DirectoryCompareMethods = new List<CompareMethodOption>
        {
            new("存在", DirectoryCompareMethod.Exists),
            new("大小", DirectoryCompareMethod.Size),
            new("大小+修改时间", DirectoryCompareMethod.SizeAndModifiedTime),
            new("完整对比", DirectoryCompareMethod.Full)
        };
        _selectedDirectoryCompareMethod = DirectoryCompareMethods.Last();
    }

    public IReadOnlyList<string> DirectoryFilterOptions { get; }
    public IReadOnlyList<string> DirectorySortOptions { get; }
    public IReadOnlyList<CompareMethodOption> DirectoryCompareMethods { get; }

    public ObservableCollection<DiffLineViewModel> FileDiffLines { get; } = new();

    public ObservableCollection<DirectoryItemViewModel> DirectoryItems { get; } = new();

    public string LeftPath
    {
        get => _leftPath;
        set => SetField(ref _leftPath, value);
    }

    public string RightPath
    {
        get => _rightPath;
        set => SetField(ref _rightPath, value);
    }

    public bool IgnoreCase
    {
        get => _ignoreCase;
        set => SetField(ref _ignoreCase, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetField(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
                ScheduleToastDismiss(value);
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public IBrush ToastBackgroundBrush => _toastKind switch
    {
        ToastKind.Success => new SolidColorBrush(Color.Parse("#14532D")),
        ToastKind.Warning => new SolidColorBrush(Color.Parse("#7C2D12")),
        ToastKind.Error => new SolidColorBrush(Color.Parse("#7F1D1D")),
        _ => new SolidColorBrush(Color.Parse("#163A39"))
    };

    public IBrush ToastBorderBrush => _toastKind switch
    {
        ToastKind.Success => new SolidColorBrush(Color.Parse("#22C55E")),
        ToastKind.Warning => new SolidColorBrush(Color.Parse("#F97316")),
        ToastKind.Error => new SolidColorBrush(Color.Parse("#EF4444")),
        _ => new SolidColorBrush(Color.Parse("#2F6F69"))
    };

    public IBrush ToastForegroundBrush => new SolidColorBrush(Color.Parse("#F8FAFC"));

    public string Summary
    {
        get => _summary;
        set
        {
            if (SetField(ref _summary, value))
            {
                OnPropertyChanged(nameof(HasSummary));
            }
        }
    }

    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

    public int SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetField(ref _selectedTab, value))
            {
                OnPropertyChanged(nameof(IsDirectoryTabSelected));
                OnPropertyChanged(nameof(IsFileTabSelected));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public bool HasFileDiff => FileDiffLines.Count > 0;

    public bool HasDirectoryDiff => DirectoryItems.Count > 0;

    public bool IsDirectoryTabSelected
    {
        get => SelectedTab == 0;
        set
        {
            if (value)
            {
                SelectedTab = 0;
            }
        }
    }

    public bool IsFileTabSelected
    {
        get => SelectedTab == 1;
        set
        {
            if (value)
            {
                SelectedTab = 1;
            }
        }
    }

    public CompareMethodOption SelectedDirectoryCompareMethod
    {
        get => _selectedDirectoryCompareMethod;
        set => SetField(ref _selectedDirectoryCompareMethod, value);
    }

    public string DirectoryFilter
    {
        get => _directoryFilter;
        set
        {
            if (SetField(ref _directoryFilter, value))
            {
                ApplyDirectoryFilter();
            }
        }
    }

    public string DirectorySearchText
    {
        get => _directorySearchText;
        set
        {
            if (SetField(ref _directorySearchText, value))
            {
                ApplyDirectoryFilter();
            }
        }
    }

    public string DirectorySort
    {
        get => _directorySort;
        set
        {
            if (SetField(ref _directorySort, value))
            {
                ApplyDirectoryFilter();
            }
        }
    }

    public bool DirectorySortDescending
    {
        get => _directorySortDescending;
        set
        {
            if (SetField(ref _directorySortDescending, value))
            {
                ApplyDirectoryFilter();
            }
        }
    }

    public string DirectoryCountText
    {
        get => _directoryCountText;
        private set => SetField(ref _directoryCountText, value);
    }

    private void ScheduleToastDismiss(string message)
    {
        _toastCancellationTokenSource?.Cancel();
        _toastCancellationTokenSource?.Dispose();
        _toastCancellationTokenSource = null;

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        _toastCancellationTokenSource = cancellationTokenSource;
        _ = DismissToastAsync(cancellationTokenSource.Token);
    }

    private void ShowToast(string message, ToastKind toastKind = ToastKind.Info)
    {
        _toastKind = toastKind;
        OnPropertyChanged(nameof(ToastBackgroundBrush));
        OnPropertyChanged(nameof(ToastBorderBrush));
        OnPropertyChanged(nameof(ToastForegroundBrush));
        StatusMessage = message;
    }

    private async Task DismissToastAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2.4), cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                StatusMessage = string.Empty;
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    public async Task CompareAsync()
    {
        if (string.IsNullOrWhiteSpace(LeftPath) || string.IsNullOrWhiteSpace(RightPath))
        {
            ShowToast("请先选择左右路径。", ToastKind.Warning);
            return;
        }

        IsBusy = true;
        ShowToast("正在对比...");
        Summary = string.Empty;
        FileDiffLines.Clear();
        DirectoryItems.Clear();
        _allDirectoryItems.Clear();
        DirectoryCountText = "总数 0 | 相同 0 | 不同 0";
        OnPropertyChanged(nameof(HasFileDiff));
        OnPropertyChanged(nameof(HasDirectoryDiff));

        try
        {
            var options = new DiffOptions
            {
                IgnoreCase = IgnoreCase,
                DirectoryCompareMethod = SelectedDirectoryCompareMethod.Method
            };

            var result = await Task.Run(() => DiffEngine.Compare(LeftPath, RightPath, GetRequestedCompareMode(), options));

            if (result.Kind == ComparisonKind.File && result.File is not null)
            {
                foreach (var line in result.File.Lines)
                {
                    FileDiffLines.Add(new DiffLineViewModel(line));
                }

                Summary = result.File.Summary;
                ShowToast(result.File.AreEqual ? "文件完全一致。" : "文件存在差异。", result.File.AreEqual ? ToastKind.Success : ToastKind.Warning);
                SelectedTab = 1;
            }
            else if (result.Kind == ComparisonKind.Directory && result.Directory is not null)
            {
                _allDirectoryItems.Clear();
                foreach (var item in result.Directory.Items)
                {
                    _allDirectoryItems.Add(new DirectoryItemViewModel(item));
                }

                Summary = $"新增 {result.Directory.Summary.Added} / 删除 {result.Directory.Summary.Removed} / 修改 {result.Directory.Summary.Modified} / 相同 {result.Directory.Summary.Same}";
                ShowToast(result.Directory.Items.Count == 0 ? "目录完全一致。" : "目录存在差异。", result.Directory.Items.Count == 0 ? ToastKind.Success : ToastKind.Warning);
                SelectedTab = 0;
                UpdateDirectoryCounts();
                ApplyDirectoryFilter();
            }
            else
            {
                ShowToast("未能生成对比结果。", ToastKind.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowToast(ex.Message, ToastKind.Error);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasFileDiff));
            OnPropertyChanged(nameof(HasDirectoryDiff));
        }
    }

    private CompareMode GetRequestedCompareMode()
    {
        return SelectedTab switch
        {
            1 => CompareMode.File,
            _ => CompareMode.Directory
        };
    }

    public void SwapPaths()
    {
        (LeftPath, RightPath) = (RightPath, LeftPath);
    }

    public async Task CopyRightToLeftAsync(DirectoryItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(LeftPath) || string.IsNullOrWhiteSpace(RightPath))
        {
            ShowToast("请先选择左右路径。", ToastKind.Warning);
            return;
        }

        if (!item.IsOnlyRight)
        {
            ShowToast("仅右侧存在时才能复制到左侧。", ToastKind.Warning);
            return;
        }

        var source = Path.Combine(RightPath, item.RelativePath);
        var target = Path.Combine(LeftPath, item.RelativePath);

        try
        {
            if (item.IsDirectory)
            {
                CopyDirectory(source, target);
            }
            else
            {
                var parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                File.Copy(source, target, true);
            }

            ShowToast("已复制到左侧。", ToastKind.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"复制失败: {ex.Message}", ToastKind.Error);
            return;
        }

        await CompareAsync();
    }

    public async Task DeleteRightAsync(DirectoryItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(RightPath))
        {
            ShowToast("请先选择右侧路径。", ToastKind.Warning);
            return;
        }

        if (!item.IsOnlyRight)
        {
            ShowToast("仅右侧存在时才能删除。", ToastKind.Warning);
            return;
        }

        var target = Path.Combine(RightPath, item.RelativePath);

        try
        {
            if (item.IsDirectory)
            {
                Directory.Delete(target, true);
            }
            else
            {
                File.Delete(target);
            }

            ShowToast("已删除右侧条目。", ToastKind.Success);
        }
        catch (Exception ex)
        {
            ShowToast($"删除失败: {ex.Message}", ToastKind.Error);
            return;
        }

        await CompareAsync();
    }

    public void OpenParentFolders(DirectoryItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(LeftPath) || string.IsNullOrWhiteSpace(RightPath))
        {
            ShowToast("请先选择左右路径。", ToastKind.Warning);
            return;
        }

        var leftFull = Path.Combine(LeftPath, item.RelativePath);
        var rightFull = Path.Combine(RightPath, item.RelativePath);

        var leftParent = Path.GetDirectoryName(leftFull);
        var rightParent = Path.GetDirectoryName(rightFull);

        if (string.IsNullOrWhiteSpace(leftParent))
        {
            leftParent = LeftPath;
        }

        if (string.IsNullOrWhiteSpace(rightParent))
        {
            rightParent = RightPath;
        }

        if (!Directory.Exists(leftParent))
        {
            leftParent = LeftPath;
        }

        if (!Directory.Exists(rightParent))
        {
            rightParent = RightPath;
        }

        OpenFolderIfExists(leftParent);
        OpenFolderIfExists(rightParent);
    }

    public void OpenSingleSideItem(DirectoryItemViewModel item)
    {
        if (!item.IsOnlyLeft && !item.IsOnlyRight)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(LeftPath) || string.IsNullOrWhiteSpace(RightPath))
        {
            ShowToast("请先选择左右路径。", ToastKind.Warning);
            return;
        }

        var targetPath = item.IsOnlyLeft
            ? Path.Combine(LeftPath, item.RelativePath)
            : Path.Combine(RightPath, item.RelativePath);

        if (!OpenPathIfExists(targetPath))
        {
            ShowToast("目标不存在，无法打开。", ToastKind.Error);
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(directory));
            CopyDirectory(directory, dest);
        }
    }

    private static void OpenFolderIfExists(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        OpenPathIfExists(path);
    }

    private static bool OpenPathIfExists(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return false;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", path);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        else
        {
            Process.Start("xdg-open", path);
        }

        return true;
    }

    private void UpdateDirectoryCounts()
    {
        var total = _allDirectoryItems.Count;
        var same = _allDirectoryItems.Count(item => item.Status == DirectoryDiffStatus.Same);
        var diff = total - same;
        DirectoryCountText = $"总数 {total} | 相同 {same} | 不同 {diff}";
    }

    private void ApplyDirectoryFilter()
    {
        IEnumerable<DirectoryItemViewModel> query = _allDirectoryItems;

        query = _directoryFilter switch
        {
            "相同" => query.Where(item => item.Status == DirectoryDiffStatus.Same),
            "不同" => query.Where(item => item.Status != DirectoryDiffStatus.Same),
            "新增" => query.Where(item => item.Status == DirectoryDiffStatus.Added),
            "删除" => query.Where(item => item.Status == DirectoryDiffStatus.Removed),
            "修改" => query.Where(item => item.Status == DirectoryDiffStatus.Modified),
            "类型" => query.Where(item => item.Status == DirectoryDiffStatus.TypeChanged),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(_directorySearchText))
        {
            query = query.Where(item =>
                item.DisplayPath.Contains(_directorySearchText, StringComparison.OrdinalIgnoreCase));
        }

        query = _directorySort switch
        {
            "状态" => query.OrderBy(item => item.Status),
            "左大小" => query.OrderBy(item => item.LeftSizeValue ?? -1),
            "右大小" => query.OrderBy(item => item.RightSizeValue ?? -1),
            "左修改时间" => query.OrderBy(item => item.LeftModifiedValue ?? DateTime.MinValue),
            "右修改时间" => query.OrderBy(item => item.RightModifiedValue ?? DateTime.MinValue),
            _ => query.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
        };

        if (_directorySortDescending)
        {
            query = query.Reverse();
        }

        DirectoryItems.Clear();
        foreach (var item in query)
        {
            DirectoryItems.Add(item);
        }

        OnPropertyChanged(nameof(HasDirectoryDiff));
    }

    public sealed class CompareMethodOption
    {
        public CompareMethodOption(string displayName, DirectoryCompareMethod method)
        {
            DisplayName = displayName;
            Method = method;
        }

        public string DisplayName { get; }
        public DirectoryCompareMethod Method { get; }

        public override string ToString() => DisplayName;
    }
}
