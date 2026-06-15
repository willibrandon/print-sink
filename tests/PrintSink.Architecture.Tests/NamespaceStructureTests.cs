using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests namespace-to-folder structure rules.
/// </summary>
[TestClass]
internal sealed class NamespaceStructureTests
{
    /// <summary>
    /// Gets or sets the current test context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies source namespaces match the project root namespace plus the relative folder path.
    /// </summary>
    [TestMethod]
    public async Task CSharpTypesUseNamespacesMatchingTheirProjectFolder()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string[] sourceFiles = SourceFileDiscovery.EnumerateRepositorySourceFiles(repositoryRoot);

        List<string> failures = [];
        foreach (string sourceFile in sourceFiles)
        {
            string expectedNamespace = SourceFileDiscovery.GetExpectedNamespace(sourceFile);
            string sourceText = await File
                .ReadAllTextAsync(sourceFile, TestContext.CancellationToken)
                .ConfigureAwait(false);
            CompilationUnitSyntax root = CSharpSyntaxTree
                .ParseText(sourceText, path: sourceFile, cancellationToken: TestContext.CancellationToken)
                .GetCompilationUnitRoot(TestContext.CancellationToken);

            foreach (BaseTypeDeclarationSyntax declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                string? actualNamespace = declaration
                    .Ancestors()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .FirstOrDefault()
                    ?.Name
                    .ToString();

                if (!string.Equals(actualNamespace, expectedNamespace, StringComparison.Ordinal))
                {
                    failures.Add(
                        $"{SourceFileDiscovery.RelativePath(repositoryRoot, sourceFile)} declares '{actualNamespace ?? "<global>"}' but expected '{expectedNamespace}'.");
                }
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Verifies design documentation names the namespaces that exist in the repository.
    /// </summary>
    [TestMethod]
    public async Task DesignDocumentUsesActualNamespaceShape()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string designPath = Path.Combine(repositoryRoot, "docs", "DESIGN.md");
        string design = await File
            .ReadAllTextAsync(designPath, TestContext.CancellationToken)
            .ConfigureAwait(false);

        Assert.Contains("`PrintSink.App`", design);
        Assert.DoesNotContain("PrintSink.App.Screens", design);
    }
}
