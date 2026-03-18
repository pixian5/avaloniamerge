namespace AvaloniaMerge.Core;

public sealed class DiffOptions
{
    public bool IgnoreCase { get; set; }
    public DirectoryCompareMethod DirectoryCompareMethod { get; set; } = DirectoryCompareMethod.Full;

    public static DiffOptions Default { get; } = new();
}
