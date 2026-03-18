namespace AvaloniaMerge.Core;

public sealed record DirectoryDiffSummary(int Added, int Removed, int Modified, int Same, int TypeChanged);
