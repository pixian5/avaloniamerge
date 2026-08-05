using System.Text;

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
        var leftIsText = TryReadTextFile(leftPath, out var leftText);
        var rightIsText = TryReadTextFile(rightPath, out var rightText);

        if (!leftIsText || !rightIsText)
        {
            var same = FilesEqual(leftPath, rightPath);
            return new FileDiffResult(true, same, Array.Empty<DiffLine>(), same ? $"{summaryPrefix} (二进制相同)" : $"{summaryPrefix} (二进制不同)");
        }

        var comparer = options.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var lines = BuildLineDiff(leftText!.Lines, rightText!.Lines, comparer);
        var added = lines.Count(line => line.Kind == DiffKind.Added);
        var removed = lines.Count(line => line.Kind == DiffKind.Removed);
        var unchanged = lines.Count(line => line.Kind == DiffKind.Unchanged);
        var lineEndingDifferent = leftText.NewLineStyle != rightText.NewLineStyle ||
            leftText.HasFinalNewLine != rightText.HasFinalNewLine;
        var areEqual = added == 0 && removed == 0 && !lineEndingDifferent;
        var summary = $"{summaryPrefix} | +{added} -{removed} ={unchanged}";

        if (lineEndingDifferent)
        {
            summary += " | 换行符不同";
        }

        return new FileDiffResult(false, areEqual, lines, summary);
    }

    public static DirectoryDiffResult CompareDirectories(string leftDir, string rightDir, DiffOptions options)
    {
        var comparer = options.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

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
                items.Add(new DirectoryDiffItem(key, true, DirectoryDiffStatus.Same, true, true, null, null,
                    leftInfo.Exists ? leftInfo.LastWriteTime : null, rightInfo.Exists ? rightInfo.LastWriteTime : null));
            }
            else if (leftIsFile && rightIsFile)
            {
                var leftInfo = new FileInfo(leftPath!);
                var rightInfo = new FileInfo(rightPath!);
                var same = AreFilesSame(leftPath!, rightPath!, leftInfo, rightInfo, options.DirectoryCompareMethod);
                items.Add(new DirectoryDiffItem(key, false, same ? DirectoryDiffStatus.Same : DirectoryDiffStatus.Modified,
                    true, true, leftInfo.Length, rightInfo.Length, leftInfo.LastWriteTime, rightInfo.LastWriteTime));
            }
            else if ((leftIsDir && rightIsFile) || (leftIsFile && rightIsDir))
            {
                var leftModified = leftIsFile
                    ? new FileInfo(leftPath!).LastWriteTime
                    : new DirectoryInfo(Path.Combine(leftDir, key)).LastWriteTime;
                var rightModified = rightIsFile
                    ? new FileInfo(rightPath!).LastWriteTime
                    : new DirectoryInfo(Path.Combine(rightDir, key)).LastWriteTime;
                items.Add(new DirectoryDiffItem(key, leftIsDir || rightIsDir, DirectoryDiffStatus.TypeChanged,
                    true, true, leftIsFile ? new FileInfo(leftPath!).Length : null, rightIsFile ? new FileInfo(rightPath!).Length : null,
                    leftModified, rightModified));
            }
            else if (leftIsDir)
            {
                var leftInfo = new DirectoryInfo(Path.Combine(leftDir, key));
                items.Add(new DirectoryDiffItem(key, true, DirectoryDiffStatus.Removed, true, false, null, null,
                    leftInfo.Exists ? leftInfo.LastWriteTime : null, null));
            }
            else if (rightIsDir)
            {
                var rightInfo = new DirectoryInfo(Path.Combine(rightDir, key));
                items.Add(new DirectoryDiffItem(key, true, DirectoryDiffStatus.Added, false, true, null, null, null,
                    rightInfo.Exists ? rightInfo.LastWriteTime : null));
            }
            else if (leftIsFile)
            {
                var leftInfo = new FileInfo(leftPath!);
                items.Add(new DirectoryDiffItem(key, false, DirectoryDiffStatus.Removed, true, false, leftInfo.Length, null,
                    leftInfo.LastWriteTime, null));
            }
            else if (rightIsFile)
            {
                var rightInfo = new FileInfo(rightPath!);
                items.Add(new DirectoryDiffItem(key, false, DirectoryDiffStatus.Added, false, true, null, rightInfo.Length,
                    null, rightInfo.LastWriteTime));
            }
        }

        var changedPaths = new HashSet<string>(items.Where(item => item.Status != DirectoryDiffStatus.Same)
            .Select(item => item.RelativePath), comparer);
        foreach (var item in items.Where(item => item.Status != DirectoryDiffStatus.Same).ToArray())
        {
            var parent = Path.GetDirectoryName(item.RelativePath);
            while (!string.IsNullOrEmpty(parent))
            {
                changedPaths.Add(parent);
                parent = Path.GetDirectoryName(parent);
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].IsDirectory && items[i].Status == DirectoryDiffStatus.Same && changedPaths.Contains(items[i].RelativePath))
            {
                items[i] = items[i] with { Status = DirectoryDiffStatus.Modified };
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
        var trace = new List<Dictionary<int, int>>();
        var max = leftLines.Length + rightLines.Length;
        var previous = new Dictionary<int, int> { [1] = 0 };

        for (var distance = 0; distance <= max; distance++)
        {
            var current = new Dictionary<int, int>();
            for (var diagonal = -distance; diagonal <= distance; diagonal += 2)
            {
                var down = GetDiagonal(previous, diagonal + 1);
                var right = GetDiagonal(previous, diagonal - 1) + 1;
                var x = diagonal == -distance || (diagonal != distance && down > right) ? down : right;
                var y = x - diagonal;

                while (x < leftLines.Length && y < rightLines.Length && comparer.Equals(leftLines[x], rightLines[y]))
                {
                    x++;
                    y++;
                }

                current[diagonal] = x;
                if (x >= leftLines.Length && y >= rightLines.Length)
                {
                    trace.Add(current);
                    return BuildLineDiffResult(leftLines, rightLines, trace);
                }
            }

            trace.Add(current);
            previous = current;
        }

        return Array.Empty<DiffLine>();
    }

    private static IReadOnlyList<DiffLine> BuildLineDiffResult(string[] leftLines, string[] rightLines, List<Dictionary<int, int>> trace)
    {
        var operations = new List<(DiffKind Kind, int? LeftIndex, int? RightIndex)>();
        var x = leftLines.Length;
        var y = rightLines.Length;

        for (var distance = trace.Count - 1; distance > 0; distance--)
        {
            var diagonal = x - y;
            var previous = trace[distance - 1];
            var down = GetDiagonal(previous, diagonal + 1);
            var right = GetDiagonal(previous, diagonal - 1) + 1;
            var previousDiagonal = diagonal == -distance || (diagonal != distance && down > right)
                ? diagonal + 1
                : diagonal - 1;
            var previousX = GetDiagonal(previous, previousDiagonal);
            var previousY = previousX - previousDiagonal;

            while (x > previousX && y > previousY)
            {
                operations.Add((DiffKind.Unchanged, x - 1, y - 1));
                x--;
                y--;
            }

            if (x == previousX)
            {
                operations.Add((DiffKind.Added, null, y - 1));
                y--;
            }
            else
            {
                operations.Add((DiffKind.Removed, x - 1, null));
                x--;
            }
        }

        while (x > 0 && y > 0)
        {
            operations.Add((DiffKind.Unchanged, x - 1, y - 1));
            x--;
            y--;
        }

        while (x > 0)
        {
            operations.Add((DiffKind.Removed, x - 1, null));
            x--;
        }

        while (y > 0)
        {
            operations.Add((DiffKind.Added, null, y - 1));
            y--;
        }

        operations.Reverse();
        var result = new List<DiffLine>(operations.Count);
        var leftLineNumber = 1;
        var rightLineNumber = 1;
        foreach (var operation in operations)
        {
            result.Add(operation.Kind switch
            {
                DiffKind.Unchanged => new DiffLine(operation.Kind, leftLines[operation.LeftIndex!.Value], leftLineNumber++, rightLineNumber++),
                DiffKind.Removed => new DiffLine(operation.Kind, leftLines[operation.LeftIndex!.Value], leftLineNumber++, null),
                _ => new DiffLine(operation.Kind, rightLines[operation.RightIndex!.Value], null, rightLineNumber++)
            });
        }

        return result;
    }

    private static int GetDiagonal(IReadOnlyDictionary<int, int> values, int diagonal)
    {
        return values.TryGetValue(diagonal, out var value) ? value : 0;
    }

    private static bool TryReadTextFile(string path, out TextFileContent? content)
    {
        var encoding = DetectTextEncoding(path);
        if (encoding is null)
        {
            content = null;
            return false;
        }

        using var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var hasFinalNewLine = text.EndsWith('\n') || text.EndsWith('\r');
        var newLineStyle = DetectNewLineStyle(text);
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        if (hasFinalNewLine && lines.Length > 0)
        {
            lines = lines[..^1];
        }

        if (text.Length == 0)
        {
            lines = Array.Empty<string>();
        }

        content = new TextFileContent(lines, newLineStyle, hasFinalNewLine);
        return true;
    }

    private static Encoding? DetectTextEncoding(string path)
    {
        const int sampleSize = 8192;
        using var stream = File.OpenRead(path);
        var buffer = new byte[Math.Min(sampleSize, stream.Length)];
        var read = ReadChunk(stream, buffer);

        if (StartsWith(buffer, read, 0xFF, 0xFE, 0x00, 0x00))
        {
            return new UTF32Encoding(false, true);
        }

        if (StartsWith(buffer, read, 0x00, 0x00, 0xFE, 0xFF))
        {
            return new UTF32Encoding(true, true);
        }

        if (StartsWith(buffer, read, 0xEF, 0xBB, 0xBF))
        {
            return new UTF8Encoding(true);
        }

        if (StartsWith(buffer, read, 0xFF, 0xFE))
        {
            return Encoding.Unicode;
        }

        if (StartsWith(buffer, read, 0xFE, 0xFF))
        {
            return Encoding.BigEndianUnicode;
        }

        if (LooksLikeUtf16(buffer, read, littleEndian: true))
        {
            return Encoding.Unicode;
        }

        if (LooksLikeUtf16(buffer, read, littleEndian: false))
        {
            return Encoding.BigEndianUnicode;
        }

        if (buffer.AsSpan(0, read).IndexOf((byte)0) >= 0)
        {
            return null;
        }

        try
        {
            _ = new UTF8Encoding(false, true).GetString(buffer, 0, read);
            return new UTF8Encoding(false);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool LooksLikeUtf16(byte[] buffer, int length, bool littleEndian)
    {
        var pairs = length / 2;
        if (pairs < 2)
        {
            return false;
        }

        var zeroBytes = 0;
        var printableBytes = 0;
        for (var i = 0; i + 1 < length; i += 2)
        {
            var zeroIndex = littleEndian ? i + 1 : i;
            var valueIndex = littleEndian ? i : i + 1;
            if (buffer[zeroIndex] == 0)
            {
                zeroBytes++;
            }

            if (buffer[valueIndex] is >= 0x09 and <= 0x7E)
            {
                printableBytes++;
            }
        }

        var threshold = Math.Max(2, (pairs * 2 + 2) / 3);
        return zeroBytes >= threshold && printableBytes >= threshold;
    }

    private static bool StartsWith(byte[] buffer, int length, params byte[] prefix)
    {
        return length >= prefix.Length && buffer.AsSpan(0, prefix.Length).SequenceEqual(prefix);
    }

    private static string DetectNewLineStyle(string text)
    {
        var hasCrlf = text.Contains("\r\n", StringComparison.Ordinal);
        var hasLf = false;
        var hasCr = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n' && (i == 0 || text[i - 1] != '\r'))
            {
                hasLf = true;
            }
            else if (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n'))
            {
                hasCr = true;
            }
        }

        if (hasCrlf && (hasLf || hasCr))
        {
            return "混合";
        }

        return hasCrlf ? "CRLF" : hasLf ? "LF" : hasCr ? "CR" : string.Empty;
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
            var leftRead = ReadChunk(leftStream, leftBuffer);
            var rightRead = ReadChunk(rightStream, rightBuffer);
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

    private static int ReadChunk(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static bool AreFilesSame(string leftPath, string rightPath, FileInfo leftInfo, FileInfo rightInfo, DirectoryCompareMethod method)
    {
        return method switch
        {
            DirectoryCompareMethod.Exists => true,
            DirectoryCompareMethod.Size => leftInfo.Length == rightInfo.Length,
            DirectoryCompareMethod.SizeAndModifiedTime => leftInfo.Length == rightInfo.Length && leftInfo.LastWriteTimeUtc == rightInfo.LastWriteTimeUtc,
            _ => leftInfo.Length == rightInfo.Length && FilesEqual(leftPath, rightPath)
        };
    }

    private sealed record TextFileContent(string[] Lines, string NewLineStyle, bool HasFinalNewLine);
}
