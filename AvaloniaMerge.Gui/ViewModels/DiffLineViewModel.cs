using Avalonia.Media;
using AvaloniaMerge.Core;

namespace AvaloniaMerge.Gui.ViewModels;

public sealed class DiffLineViewModel
{
    public DiffLineViewModel(DiffLine line)
    {
        Kind = line.Kind;
        Text = line.Text;
        LeftLineNumber = line.LeftLineNumber;
        RightLineNumber = line.RightLineNumber;
        DisplayText = BuildDisplayText(line);
        Background = ResolveBackground(line.Kind);
        Foreground = ResolveForeground(line.Kind);
    }

    public DiffKind Kind { get; }
    public string Text { get; }
    public int? LeftLineNumber { get; }
    public int? RightLineNumber { get; }
    public string DisplayText { get; }
    public IBrush Background { get; }
    public IBrush Foreground { get; }

    private static string BuildDisplayText(DiffLine line)
    {
        var prefix = line.Kind switch
        {
            DiffKind.Added => '+',
            DiffKind.Removed => '-',
            _ => ' '
        };

        var leftNo = line.LeftLineNumber?.ToString() ?? string.Empty;
        var rightNo = line.RightLineNumber?.ToString() ?? string.Empty;
        return $"{prefix} {leftNo,4} {rightNo,4} {line.Text}";
    }

    private static IBrush ResolveBackground(DiffKind kind)
    {
        var resources = Avalonia.Application.Current?.Resources;
        return kind switch
        {
            DiffKind.Added => (IBrush?)resources?["DiffAddedBrush"] ?? Brushes.LightGreen,
            DiffKind.Removed => (IBrush?)resources?["DiffRemovedBrush"] ?? Brushes.LightCoral,
            _ => (IBrush?)resources?["DiffUnchangedBrush"] ?? Brushes.Transparent
        };
    }

    private static IBrush ResolveForeground(DiffKind kind)
    {
        return kind switch
        {
            DiffKind.Added => Brushes.DarkGreen,
            DiffKind.Removed => Brushes.DarkRed,
            _ => Brushes.Black
        };
    }
}
