using System.Text;
using AvaloniaMerge.Core;

var root = Path.Combine(Path.GetTempPath(), $"avalonia-merge-tests-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);

try
{
    TestTextDiffAndLineEndings(root);
    TestUtf16Text(root);
    TestDirectoryComparison(root);
    TestLargeLineDiff(root);
    Console.WriteLine("All AvaloniaMerge tests passed.");
    return 0;
}
finally
{
    if (Directory.Exists(root))
    {
        Directory.Delete(root, true);
    }
}

static void TestTextDiffAndLineEndings(string root)
{
    var left = Path.Combine(root, "newline-left.txt");
    var right = Path.Combine(root, "newline-right.txt");
    File.WriteAllText(left, "same\n");
    File.WriteAllText(right, "same");

    var result = DiffEngine.Compare(left, right, CompareMode.File);
    Assert(!result.File!.AreEqual, "A missing final newline must be reported as a difference.");
    Assert(result.File.Summary.Contains("换行符不同", StringComparison.Ordinal), "The summary must explain the line-ending difference.");
}

static void TestUtf16Text(string root)
{
    var left = Path.Combine(root, "utf16-left.txt");
    var right = Path.Combine(root, "utf16-right.txt");
    File.WriteAllText(left, "A\nB\n", Encoding.Unicode);
    File.WriteAllText(right, "A\nC\n", Encoding.Unicode);

    var result = DiffEngine.Compare(left, right, CompareMode.File);
    Assert(!result.File!.IsBinary, "UTF-16 text must not be treated as binary.");
    Assert(result.File.Lines.Any(line => line.Kind == DiffKind.Removed && line.Text == "B"), "UTF-16 removed lines must be visible.");
    Assert(result.File.Lines.Any(line => line.Kind == DiffKind.Added && line.Text == "C"), "UTF-16 added lines must be visible.");

    File.WriteAllBytes(left, [0x41, 0x00, 0x0A, 0x00]);
    File.WriteAllBytes(right, [0x41, 0x00, 0x0A, 0x00]);
    result = DiffEngine.Compare(left, right, CompareMode.File);
    Assert(!result.File!.IsBinary && result.File.AreEqual, "Short UTF-16 text without a BOM must be recognized.");
}

static void TestDirectoryComparison(string root)
{
    var left = Path.Combine(root, "directory-left");
    var right = Path.Combine(root, "directory-right");
    Directory.CreateDirectory(Path.Combine(left, "sub"));
    Directory.CreateDirectory(Path.Combine(right, "sub"));
    File.WriteAllText(Path.Combine(left, "sub", "same.txt"), "same");
    File.WriteAllText(Path.Combine(right, "sub", "same.txt"), "same");

    var same = DiffEngine.Compare(left, right, CompareMode.Directory);
    Assert(same.Directory!.Summary.Added == 0 && same.Directory.Summary.Removed == 0 &&
        same.Directory.Summary.Modified == 0 && same.Directory.Summary.TypeChanged == 0,
        "Identical directories must have no difference statuses.");

    File.WriteAllText(Path.Combine(right, "sub", "same.txt"), "different");
    var different = DiffEngine.Compare(left, right, CompareMode.Directory);
    Assert(different.Directory!.Summary.Modified == 2, "A changed file must also mark its parent directory as modified.");
}

static void TestLargeLineDiff(string root)
{
    var left = Path.Combine(root, "large-left.txt");
    var right = Path.Combine(root, "large-right.txt");
    var lines = Enumerable.Range(0, 5000).Select(index => $"line-{index}").ToArray();
    File.WriteAllLines(left, lines);
    File.WriteAllLines(right, lines);

    var result = DiffEngine.Compare(left, right, CompareMode.File);
    Assert(result.File!.AreEqual && result.File.Lines.Count == lines.Length, "Large equal files must compare successfully.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
