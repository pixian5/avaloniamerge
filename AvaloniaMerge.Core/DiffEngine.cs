namespace AvaloniaMerge.Core;

public static class DiffEngine
{
    public static ComparisonResult Compare(string leftPath, string rightPath, CompareMode mode, DiffOptions? options = null)
    {
        options ??= DiffOptions.Default;

        if (string.IsNullOrWhiteSpace(leftPath) || string.IsNullOrWhiteSpace(rightPath))
        {
            throw new ArgumentException("左右路径不能为空。", nameof(leftPath));
        }

        var leftIsDir = Directory.Exists(leftPath);
        var rightIsDir = Directory.Exists(rightPath);
        var leftIsFile = File.Exists(leftPath);
        var rightIsFile = File.Exists(rightPath);

        if (!leftIsDir && !leftIsFile)
        {
            throw new FileNotFoundException($"找不到左侧路径: {leftPath}");
        }

        if (!rightIsDir && !rightIsFile)
        {
            throw new FileNotFoundException($"找不到右侧路径: {rightPath}");
        }

        if (leftIsDir != rightIsDir)
        {
            throw new InvalidOperationException("左右类型不同：一个是目录，一个是文件。");
        }

        if (mode == CompareMode.Auto)
        {
            mode = leftIsDir ? CompareMode.Directory : CompareMode.File;
        }

        if (mode == CompareMode.File && leftIsDir)
        {
            throw new InvalidOperationException("文件模式下需要选择两个文件。");
        }

        if (mode == CompareMode.Directory && leftIsFile)
        {
            throw new InvalidOperationException("目录模式下需要选择两个目录。");
        }

        return mode switch
        {
            CompareMode.File => new ComparisonResult(ComparisonKind.File, CompareFiles(leftPath, rightPath, options), null),
            CompareMode.Directory => new ComparisonResult(ComparisonKind.Directory, null, CompareDirectories(leftPath, rightPath, options)),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "不支持的对比模式")
        };
    }

    public static FileDiffResult CompareFiles(string leftPath, string rightPath, DiffOptions options)
    {
        var summaryPrefix = $"{Path.GetFileName(leftPath)} ↔ {Path.GetFileName(rightPath)}";

        if (IsBinary(leftPath) || IsBinary(rightPath))
        {
            var same = FilesEqual(leftPath, rightPath);
            return new FileDiffResult(true, same, Array.Empty<DiffLine>(), same ? $"{summaryPrefix} (二进制相同)" : $"{summaryPrefix} (二进制不同)");
        }

        var leftLines = File.ReadAllLines(leftPath);
        var rightLines = File.ReadAllLines(rightPath);
        var comparer = options.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var lines = BuildLineDiff(leftLines, rightLines, comparer);

        var added = lines.Count(line => line.Kind == DiffKind.Added);
        var removed = lines.Count(line => line.Kind == DiffKind.Removed);
        var unchanged = lines.Count(line => line.Kind == DiffKind.Unchanged);

        var areEqual = added == 0 && removed == 0;
        var summary = $"{summaryPrefix} | +{added} -{removed} ={unchanged}";

        return new FileDiffResult(false, areEqual, lines, summary);
    }

    public static DirectoryDiffResult CompareDirectories(string leftDir, string rightDir, DiffOptions options)
    {
        var comparer = options.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var comparison = options.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        var leftFiles = Directory.GetFiles(leftDir, "*", SearchOption.AllDirectories);
        var rightFiles = Directory.GetFiles(rightDir, "*", SearchOption.AllDirectories);
        var leftDirs = Directory.GetDirectories(leftDir, "*", SearchOption.AllDirectories);
        var rightDirs = Directory.GetDirectories(rightDir, "*", SearchOption.AllDirectories);

        var leftFileMap = leftFiles.ToDictionary(path => Path.GetRelativePath(leftDir, path), path => path, comparer);
        var rightFileMap = rightFiles.ToDictionary(path => Path.GetRelativePath(rightDir, path), path => path, comparer);
        var leftDirSet = new HashSet<string>(leftDirs.Select(path => Path.GetRelativePath(leftDir, path)), comparer);
        var rightDirSet = new HashSet<string>(rightDirs.Select(path => Path.GetRelativePath(rightDir, path)), comparer);

        var allKeys = new HashSet<string>(comparer);
        allKeys.UnionWith(leftFileMap.Keys);
        allKeys.UnionWith(rightFileMap.Keys);
        allKeys.UnionWith(leftDirSet);
        allKeys.UnionWith(rightDirSet);

        var items = new List<DirectoryDiffItem>(allKeys.Count);
        foreach (var key in allKeys.OrderBy(key => key, comparer))
        {
            var leftIsDir = leftDirSet.Contains(key);
            var rightIsDir = rightDirSet.Contains(key);
            var leftIsFile = leftFileMap.TryGetValue(key, out var leftPath);
            var rightIsFile = rightFileMap.TryGetValue(key, out var rightPath);

            if (leftIsDir && rightIsDir)
            {
                var leftInfo = new DirectoryInfo(Path.Combine(leftDir, key));
                var rightInfo = new DirectoryInfo(Path.Combine(rightDir, key));
                items.Add(new DirectoryDiffItem(
                    key,
                    true,
                    DirectoryDiffStatus.Same,
                    true,
                    true,
                    null,
                    null,
                    leftInfo.Exists ? leftInfo.LastWriteTime : null,
                    rightInfo.Exists ? rightInfo.LastWriteTime : null));
            }
            else if (leftIsFile && rightIsFile)
            {
                var leftInfo = new FileInfo(leftPath!);
                var rightInfo = new FileInfo(rightPath!);

                var same = AreFilesSame(leftPath!, rightPath!, leftInfo, rightInfo, options.DirectoryCompareMethod);
                items.Add(new DirectoryDiffItem(
                    key,
                    false,
                    same ? DirectoryDiffStatus.Same : DirectoryDiffStatus.Modified,
                    true,
                    true,
                    leftInfo.Length,
                    rightInfo.Length,
                    leftInfo.LastWriteTime,
                    rightInfo.LastWriteTime));
            }
            else if ((leftIsDir && rightIsFile) || (leftIsFile && rightIsDir))
            {
                long? leftSize = leftIsFile ? new FileInfo(leftPath!).Length : null;
                long? rightSize = rightIsFile ? new FileInfo(rightPath!).Length : null;
                DateTime? leftModified = leftIsFile
                    ? new FileInfo(leftPath!).LastWriteTime
                    : new DirectoryInfo(Path.Combine(leftDir, key)).LastWriteTime;
                DateTime? rightModified = rightIsFile
                    ? new FileInfo(rightPath!).LastWriteTime
                    : new DirectoryInfo(Path.Combine(rightDir, key)).LastWriteTime;
                items.Add(new DirectoryDiffItem(
                    key,
                    leftIsDir || rightIsDir,
                    DirectoryDiffStatus.TypeChanged,
                    leftIsDir || leftIsFile,
                    rightIsDir || rightIsFile,
                    leftSize,
                    rightSize,
                    leftModified,
                    rightModified));
            }
            else if (leftIsDir)
            {
                var leftInfo = new DirectoryInfo(Path.Combine(leftDir, key));
                items.Add(new DirectoryDiffItem(
                    key,
                    true,
                    DirectoryDiffStatus.Removed,
                    true,
                    false,
                    null,
                    null,
                    leftInfo.Exists ? leftInfo.LastWriteTime : null,
                    null));
            }
            else if (rightIsDir)
            {
                var rightInfo = new DirectoryInfo(Path.Combine(rightDir, key));
                items.Add(new DirectoryDiffItem(
                    key,
                    true,
                    DirectoryDiffStatus.Added,
                    false,
                    true,
                    null,
                    null,
                    null,
                    rightInfo.Exists ? rightInfo.LastWriteTime : null));
            }
            else if (leftIsFile)
            {
                var leftInfo = new FileInfo(leftPath!);
                items.Add(new DirectoryDiffItem(
                    key,
                    false,
                    DirectoryDiffStatus.Removed,
                    true,
                    false,
                    leftInfo.Length,
                    null,
                    leftInfo.LastWriteTime,
                    null));
            }
            else if (rightIsFile)
            {
                var rightInfo = new FileInfo(rightPath!);
                items.Add(new DirectoryDiffItem(
                    key,
                    false,
                    DirectoryDiffStatus.Added,
                    false,
                    true,
                    null,
                    rightInfo.Length,
                    null,
                    rightInfo.LastWriteTime));
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!item.IsDirectory || item.Status != DirectoryDiffStatus.Same)
            {
                continue;
            }

            var prefix = item.RelativePath + Path.DirectorySeparatorChar;
            var hasDiff = items.Any(child =>
                child.RelativePath.Length > prefix.Length &&
                child.RelativePath.StartsWith(prefix, comparison) &&
                child.Status != DirectoryDiffStatus.Same);

            if (hasDiff)
            {
                items[i] = item with { Status = DirectoryDiffStatus.Modified };
            }
        }

        var summary = new DirectoryDiffSummary(
            items.Count(item => item.Status == DirectoryDiffStatus.Added),
            items.Count(item => item.Status == DirectoryDiffStatus.Removed),
            items.Count(item => item.Status == DirectoryDiffStatus.Modified),
            items.Count(item => item.Status == DirectoryDiffStatus.Same),
            items.Count(item => item.Status == DirectoryDiffStatus.TypeChanged));

        return new DirectoryDiffResult(items, summary);
    }

    private static IReadOnlyList<DiffLine> BuildLineDiff(string[] leftLines, string[] rightLines, StringComparer comparer)
    {
        var leftLength = leftLines.Length;
        var rightLength = rightLines.Length;
        var lcs = new int[leftLength + 1, rightLength + 1];

        for (var i = leftLength - 1; i >= 0; i--)
        {
            for (var j = rightLength - 1; j >= 0; j--)
            {
                lcs[i, j] = comparer.Equals(leftLines[i], rightLines[j])
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var result = new List<DiffLine>();
        var leftIndex = 0;
        var rightIndex = 0;
        var leftLineNumber = 1;
        var rightLineNumber = 1;

        while (leftIndex < leftLength && rightIndex < rightLength)
        {
            if (comparer.Equals(leftLines[leftIndex], rightLines[rightIndex]))
            {
                result.Add(new DiffLine(DiffKind.Unchanged, leftLines[leftIndex], leftLineNumber, rightLineNumber));
                leftIndex++;
                rightIndex++;
                leftLineNumber++;
                rightLineNumber++;
            }
            else if (lcs[leftIndex + 1, rightIndex] >= lcs[leftIndex, rightIndex + 1])
            {
                result.Add(new DiffLine(DiffKind.Removed, leftLines[leftIndex], leftLineNumber, null));
                leftIndex++;
                leftLineNumber++;
            }
            else
            {
                result.Add(new DiffLine(DiffKind.Added, rightLines[rightIndex], null, rightLineNumber));
                rightIndex++;
                rightLineNumber++;
            }
        }

        while (leftIndex < leftLength)
        {
            result.Add(new DiffLine(DiffKind.Removed, leftLines[leftIndex], leftLineNumber, null));
            leftIndex++;
            leftLineNumber++;
        }

        while (rightIndex < rightLength)
        {
            result.Add(new DiffLine(DiffKind.Added, rightLines[rightIndex], null, rightLineNumber));
            rightIndex++;
            rightLineNumber++;
        }

        return result;
    }

    private static bool IsBinary(string path)
    {
        const int sampleSize = 8000;
        using var stream = File.OpenRead(path);
        var buffer = new byte[Math.Min(sampleSize, stream.Length)];
        var read = stream.Read(buffer, 0, buffer.Length);

        for (var i = 0; i < read; i++)
        {
            if (buffer[i] == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool FilesEqual(string leftPath, string rightPath)
    {
        var leftInfo = new FileInfo(leftPath);
        var rightInfo = new FileInfo(rightPath);

        if (leftInfo.Length != rightInfo.Length)
        {
            return false;
        }

        const int bufferSize = 1024 * 1024;
        using var leftStream = File.OpenRead(leftPath);
        using var rightStream = File.OpenRead(rightPath);
        var leftBuffer = new byte[bufferSize];
        var rightBuffer = new byte[bufferSize];

        while (true)
        {
            var leftRead = leftStream.Read(leftBuffer, 0, leftBuffer.Length);
            var rightRead = rightStream.Read(rightBuffer, 0, rightBuffer.Length);

            if (leftRead != rightRead)
            {
                return false;
            }

            if (leftRead == 0)
            {
                return true;
            }

            if (!leftBuffer.AsSpan(0, leftRead).SequenceEqual(rightBuffer.AsSpan(0, rightRead)))
            {
                return false;
            }
        }
    }

    private static bool AreFilesSame(
        string leftPath,
        string rightPath,
        FileInfo leftInfo,
        FileInfo rightInfo,
        DirectoryCompareMethod method)
    {
        return method switch
        {
            DirectoryCompareMethod.Exists => true,
            DirectoryCompareMethod.Size => leftInfo.Length == rightInfo.Length,
            DirectoryCompareMethod.SizeAndModifiedTime => leftInfo.Length == rightInfo.Length &&
                leftInfo.LastWriteTimeUtc == rightInfo.LastWriteTimeUtc,
            _ => leftInfo.Length == rightInfo.Length && FilesEqual(leftPath, rightPath)
        };
    }
}
