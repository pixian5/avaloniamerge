namespace AvaloniaMerge.Core;

public sealed record DirectoryDiffItem(
    string RelativePath,
    bool IsDirectory,
    DirectoryDiffStatus Status,
    bool LeftExists,
    bool RightExists,
    long? LeftSize,
    long? RightSize,
    DateTime? LeftModified,
    DateTime? RightModified);
