using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests namespace-to-folder structure rules.
/// </summary>
[TestClass]
public sealed class NamespaceStructureTests
{
    /// <summary>
    /// Verifies source namespaces match the project root namespace plus the relative folder path.
    /// </summary>
    [TestMethod]
    public void CSharp_types_use_namespaces_matching_their_project_folder()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string[] sourceFiles = SourceFileDiscovery.EnumerateRepositorySourceFiles(repositoryRoot);

        List<string> failures = [];
        foreach (string sourceFile in sourceFiles)
        {
            string expectedNamespace = SourceFileDiscovery.GetExpectedNamespace(sourceFile);
            CompilationUnitSyntax root = CSharpSyntaxTree
                .ParseText(File.ReadAllText(sourceFile))
                .GetCompilationUnitRoot();

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
}
