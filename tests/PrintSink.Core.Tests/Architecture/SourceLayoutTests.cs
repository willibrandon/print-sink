using System.Text.RegularExpressions;

namespace PrintSink.Core.Tests.Architecture;

/// <summary>
/// Tests repository source layout rules that are not enforced by the C# compiler.
/// </summary>
[TestClass]
public sealed partial class SourceLayoutTests
{
    /// <summary>
    /// Verifies production C# files contain at most one declared type.
    /// </summary>
    [TestMethod]
    public void ProductionFilesDeclareAtMostOneType()
    {
        string root = FindRepositoryRoot();
        string sourceRoot = Path.Combine(root, "src");
        string[] violations = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => TypeDeclarationRegex().Count(File.ReadAllText(path)) > 1)
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PrintSink.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }

    [GeneratedRegex(@"\b(public|internal|private|protected|file)?\s*(abstract|sealed|static|partial|readonly|record)?\s*(class|struct|interface|enum|record|delegate)\s+[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled)]
    private static partial Regex TypeDeclarationRegex();
}
