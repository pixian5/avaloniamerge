namespace AvaloniaMerge.Core;

public enum ComparisonKind
{
    File,
    Directory
}

public sealed record ComparisonResult(ComparisonKind Kind, FileDiffResult? File, DirectoryDiffResult? Directory);
