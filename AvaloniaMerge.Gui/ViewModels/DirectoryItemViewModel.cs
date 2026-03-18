using Avalonia.Media;
using AvaloniaMerge.Core;

namespace AvaloniaMerge.Gui.ViewModels;

public sealed class DirectoryItemViewModel
{
    public DirectoryItemViewModel(DirectoryDiffItem item)
    {
        RelativePath = item.RelativePath;
        IsDirectory = item.IsDirectory;
        DisplayPath = item.IsDirectory ? $"{item.RelativePath}/" : item.RelativePath;
        Status = item.Status;
        StatusLabel = BuildCombinedStatusLabel(item);
        StatusBrush = ResolveCombinedStatusBrush(item);
        LeftExists = item.LeftExists;
        RightExists = item.RightExists;
        IsOnlyLeft = item.LeftExists && !item.RightExists;
        IsOnlyRight = !item.LeftExists && item.RightExists;
        IsSame = item.LeftExists && item.RightExists && item.Status == DirectoryDiffStatus.Same;
        IsDifferent = item.LeftExists && item.RightExists && item.Status != DirectoryDiffStatus.Same;
        LeftSizeValue = item.LeftSize;
        RightSizeValue = item.RightSize;
        LeftSizeDisplay = FormatSize(item.LeftSize);
        RightSizeDisplay = FormatSize(item.RightSize);
        LeftModifiedValue = item.LeftModified;
        RightModifiedValue = item.RightModified;
        LeftModifiedDisplay = FormatTime(item.LeftModified);
        RightModifiedDisplay = FormatTime(item.RightModified);
    }

    public string RelativePath { get; }
    public bool IsDirectory { get; }
    public string DisplayPath { get; }
    public DirectoryDiffStatus Status { get; }
    public string StatusLabel { get; }
    public IBrush StatusBrush { get; }
    public bool LeftExists { get; }
    public bool RightExists { get; }
    public bool IsOnlyLeft { get; }
    public bool IsOnlyRight { get; }
    public bool IsSame { get; }
    public bool IsDifferent { get; }
    public long? LeftSizeValue { get; }
    public long? RightSizeValue { get; }
    public string LeftSizeDisplay { get; }
    public string RightSizeDisplay { get; }
    public DateTime? LeftModifiedValue { get; }
    public DateTime? RightModifiedValue { get; }
    public string LeftModifiedDisplay { get; }
    public string RightModifiedDisplay { get; }

    private static string BuildCombinedStatusLabel(DirectoryDiffItem item)
    {
        if (item.LeftExists && !item.RightExists)
        {
            return "仅左侧";
        }

        if (!item.LeftExists && item.RightExists)
        {
            return "仅右侧";
        }

        return item.Status switch
        {
            DirectoryDiffStatus.Same => "相同",
            _ => "不同"
        };
    }

    private static IBrush ResolveCombinedStatusBrush(DirectoryDiffItem item)
    {
        var resources = Avalonia.Application.Current?.Resources;
        if (item.LeftExists && !item.RightExists)
        {
            return (IBrush?)resources?["StatusLeftBrush"] ?? Brushes.LightBlue;
        }

        if (!item.LeftExists && item.RightExists)
        {
            return (IBrush?)resources?["StatusRightBrush"] ?? Brushes.LightCoral;
        }

        return item.Status switch
        {
            DirectoryDiffStatus.Same => (IBrush?)resources?["StatusSameBrush"] ?? Brushes.LightGreen,
            _ => (IBrush?)resources?["StatusDiffBrush"] ?? Brushes.Gold
        };
    }

    private static string FormatSize(long? size)
    {
        if (size is null)
        {
            return string.Empty;
        }

        var value = size.Value;
        string[] units = ["B", "KB", "MB", "GB"];
        var unitIndex = 0;
        var display = (double)value;

        while (display >= 1024 && unitIndex < units.Length - 1)
        {
            display /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{value} {units[unitIndex]}" : $"{display:0.0} {units[unitIndex]}";
    }

    private static string FormatTime(DateTime? time)
    {
        return time?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
    }
}
