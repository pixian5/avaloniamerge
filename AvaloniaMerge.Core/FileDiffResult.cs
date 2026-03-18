namespace AvaloniaMerge.Core;

public sealed record FileDiffResult(bool IsBinary, bool AreEqual, IReadOnlyList<DiffLine> Lines, string Summary);
