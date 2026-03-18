namespace AvaloniaMerge.Core;

public sealed record DirectoryDiffResult(IReadOnlyList<DirectoryDiffItem> Items, DirectoryDiffSummary Summary);
