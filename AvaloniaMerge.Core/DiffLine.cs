namespace AvaloniaMerge.Core;

public sealed record DiffLine(DiffKind Kind, string Text, int? LeftLineNumber, int? RightLineNumber);
