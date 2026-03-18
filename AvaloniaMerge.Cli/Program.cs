using System.Text;
using System.Text.Json;
using AvaloniaMerge.Core;

internal static class Program
{
    private const string AppName = "AvaloniaMerge";

    public static int Main(string[] args)
    {
        var options = CliOptions.Parse(args, out var error);
        if (!string.IsNullOrEmpty(error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliOptions.Usage);
            return 1;
        }

        if (options.ShowHelp)
        {
            Console.WriteLine(CliOptions.Usage);
            return 0;
        }

        var diffOptions = new DiffOptions
        {
            IgnoreCase = options.IgnoreCase
        };

        try
        {
            var result = DiffEngine.Compare(options.LeftPath!, options.RightPath!, options.Mode, diffOptions);
            var output = options.Json
                ? JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                : FormatResult(result, options.LeftPath!, options.RightPath!);

            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                File.WriteAllText(options.OutputPath!, output);
            }
            else
            {
                Console.WriteLine(output);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{AppName} 运行失败: {ex.Message}");
            return 2;
        }
    }

    private static string FormatResult(ComparisonResult result, string leftPath, string rightPath)
    {
        return result.Kind switch
        {
            ComparisonKind.File when result.File is not null => FormatFileDiff(result.File, leftPath, rightPath),
            ComparisonKind.Directory when result.Directory is not null => FormatDirectoryDiff(result.Directory, leftPath, rightPath),
            _ => "没有可输出的对比结果。"
        };
    }

    private static string FormatFileDiff(FileDiffResult result, string leftPath, string rightPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"文件对比: {leftPath} ↔ {rightPath}");
        builder.AppendLine(result.Summary);
        builder.AppendLine(new string('-', 72));

        if (result.IsBinary)
        {
            builder.AppendLine(result.AreEqual ? "二进制文件完全相同。" : "二进制文件不同。");
            return builder.ToString();
        }

        foreach (var line in result.Lines)
        {
            var prefix = line.Kind switch
            {
                DiffKind.Added => '+',
                DiffKind.Removed => '-',
                _ => ' '
            };

            var leftNo = line.LeftLineNumber?.ToString() ?? string.Empty;
            var rightNo = line.RightLineNumber?.ToString() ?? string.Empty;
            builder.AppendLine($"{prefix} {leftNo,4} {rightNo,4} {line.Text}");
        }

        return builder.ToString();
    }

    private static string FormatDirectoryDiff(DirectoryDiffResult result, string leftPath, string rightPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"目录对比: {leftPath} ↔ {rightPath}");
        builder.AppendLine($"+{result.Summary.Added} -{result.Summary.Removed} ~{result.Summary.Modified} ={result.Summary.Same}");
        builder.AppendLine(new string('-', 72));
        builder.AppendLine("状态    路径");

        foreach (var item in result.Items)
        {
            var label = item.Status switch
            {
                DirectoryDiffStatus.Added => "新增",
                DirectoryDiffStatus.Removed => "删除",
                DirectoryDiffStatus.Modified => "修改",
                DirectoryDiffStatus.Same => "相同",
                DirectoryDiffStatus.TypeChanged => "类型",
                _ => item.Status.ToString()
            };

            var path = item.IsDirectory ? $"{item.RelativePath}/" : item.RelativePath;
            builder.AppendLine($"{label,-6} {path}");
        }

        return builder.ToString();
    }

    private sealed class CliOptions
    {
        public static string Usage => $@"{AppName} CLI 用法:
  {AppName.ToLowerInvariant()} [options] <left> <right>

选项:
  --file           强制以文件模式对比
  --dir            强制以目录模式对比
  --ignore-case    忽略大小写
  --json           输出 JSON
  --out <path>     输出到文件
  -h | --help      显示帮助
";

        public CompareMode Mode { get; private set; } = CompareMode.Auto;
        public bool IgnoreCase { get; private set; }
        public bool Json { get; private set; }
        public bool ShowHelp { get; private set; }
        public string? OutputPath { get; private set; }
        public string? LeftPath { get; private set; }
        public string? RightPath { get; private set; }

        public static CliOptions Parse(string[] args, out string? error)
        {
            var options = new CliOptions();
            var positional = new List<string>();
            error = null;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--file":
                        options.Mode = CompareMode.File;
                        break;
                    case "--dir":
                        options.Mode = CompareMode.Directory;
                        break;
                    case "--ignore-case":
                        options.IgnoreCase = true;
                        break;
                    case "--json":
                        options.Json = true;
                        break;
                    case "--out":
                        if (i + 1 >= args.Length)
                        {
                            error = "--out 需要指定输出路径。";
                            return options;
                        }
                        options.OutputPath = args[++i];
                        break;
                    case "-h":
                    case "--help":
                        options.ShowHelp = true;
                        return options;
                    default:
                        positional.Add(arg);
                        break;
                }
            }

            if (positional.Count < 2)
            {
                error = "需要提供左右两个路径。";
                return options;
            }

            options.LeftPath = positional[0];
            options.RightPath = positional[1];
            return options;
        }
    }
}
