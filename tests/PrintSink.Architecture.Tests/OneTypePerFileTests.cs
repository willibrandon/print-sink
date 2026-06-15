using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests source-level architecture rules.
/// </summary>
[TestClass]
internal sealed class OneTypePerFileTests
{
    /// <summary>
    /// Gets or sets the current test context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies each C# source file declares at most one type and uses a matching file name.
    /// </summary>
    [TestMethod]
    public async Task CSharpFilesDeclareAtMostOneTypeWithMatchingFileName()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string[] sourceFiles = SourceFileDiscovery.EnumerateRepositorySourceFiles(repositoryRoot);

        List<string> failures = [];
        foreach (string sourceFile in sourceFiles)
        {
            string sourceText = await File
                .ReadAllTextAsync(sourceFile, TestContext.CancellationToken)
                .ConfigureAwait(false);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
                sourceText,
                path: sourceFile,
                cancellationToken: TestContext.CancellationToken);
            CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot(TestContext.CancellationToken);
            string[] typeNames = [.. root
                .DescendantNodes()
                .Where(static node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
                .Select(GetTypeName)];

            if (typeNames.Length > 1)
            {
                failures.Add($"{SourceFileDiscovery.RelativePath(repositoryRoot, sourceFile)} declares {typeNames.Length} types: {string.Join(", ", typeNames)}.");
                continue;
            }

            if (typeNames.Length == 1)
            {
                string fileName = GetExpectedTypeName(sourceFile);
                if (!string.Equals(fileName, typeNames[0], StringComparison.Ordinal))
                {
                    failures.Add($"{SourceFileDiscovery.RelativePath(repositoryRoot, sourceFile)} declares '{typeNames[0]}' but the file name is '{fileName}'.");
                }
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Verifies the design document names the actual one-type-per-file enforcement mechanism.
    /// </summary>
    [TestMethod]
    public async Task DesignDocumentNamesArchitectureTestEnforcement()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string designPath = Path.Combine(repositoryRoot, "docs", "DESIGN.md");
        string design = await File
            .ReadAllTextAsync(designPath, TestContext.CancellationToken)
            .ConfigureAwait(false);

        Assert.Contains("Enforced by source-level architecture tests in CI.", design);
        Assert.DoesNotContain("Enforced via an analyzer rule", design);
    }

    private static string GetTypeName(SyntaxNode node)
    {
        return node switch
        {
            BaseTypeDeclarationSyntax declaration => declaration.Identifier.ValueText,
            DelegateDeclarationSyntax declaration => declaration.Identifier.ValueText,
            _ => throw new ArgumentOutOfRangeException(nameof(node), node, "Unsupported syntax node."),
        };
    }

    private static string GetExpectedTypeName(string sourceFile)
    {
        string fileName = Path.GetFileNameWithoutExtension(sourceFile);
        return fileName.EndsWith(".xaml", StringComparison.Ordinal)
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;
    }
}
