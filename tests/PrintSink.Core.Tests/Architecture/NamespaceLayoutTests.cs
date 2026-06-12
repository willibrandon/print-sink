namespace PrintSink.Core.Tests.Architecture;

using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed partial class NamespaceLayoutTests
{
  public TestContext TestContext { get; set; } = null!;

  [TestMethod]
  public void CoreNamespacesMatchFolders()
  {
    Assert.IsFalse(TestContext.CancellationToken.IsCancellationRequested);

    string repositoryRoot = FindRepositoryRoot();
    string coreRoot = Path.Combine(repositoryRoot, "src", "PrintSink.Core");

    foreach (string file in Directory.EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories).Where(IsAuthoredSource))
    {
      string relativeDirectory = Path.GetRelativePath(coreRoot, Path.GetDirectoryName(file)!);
      string expectedNamespace = relativeDirectory == "."
        ? "PrintSink"
        : $"PrintSink.{relativeDirectory.Replace(Path.DirectorySeparatorChar, '.')}";

      string source = File.ReadAllText(file);
      Match match = NamespacePattern().Match(source);

      Assert.IsTrue(match.Success, $"Missing namespace in {file}.");
      Assert.AreEqual(expectedNamespace, match.Groups["namespace"].Value, file);
    }
  }

  private static bool IsAuthoredSource(string file)
  {
    string relativePath = Path.GetRelativePath(Path.Combine(FindRepositoryRoot(), "src", "PrintSink.Core"), file);
    string firstSegment = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

    return firstSegment is not ("bin" or "obj");
  }

  private static string FindRepositoryRoot()
  {
    DirectoryInfo? directory = new(AppContext.BaseDirectory);

    while (directory is not null)
    {
      if (Directory.Exists(Path.Combine(directory.FullName, "src", "PrintSink.Core")))
      {
        return directory.FullName;
      }

      directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not find the repository root.");
  }

  [GeneratedRegex(@"^\s*namespace\s+(?<namespace>[A-Za-z0-9_.]+)\s*;", RegexOptions.Multiline)]
  private static partial Regex NamespacePattern();
}
