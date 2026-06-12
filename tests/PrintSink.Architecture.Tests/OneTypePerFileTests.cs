using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests source-level architecture rules.
/// </summary>
[TestClass]
public sealed class OneTypePerFileTests
{
    /// <summary>
    /// Verifies each C# source file declares at most one type and uses a matching file name.
    /// </summary>
    [TestMethod]
    public void CSharp_files_declare_at_most_one_type_with_matching_file_name()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string[] sourceFiles = SourceFileDiscovery.EnumerateRepositorySourceFiles(repositoryRoot);

        List<string> failures = [];
        foreach (string sourceFile in sourceFiles)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
            CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
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
                string fileName = Path.GetFileNameWithoutExtension(sourceFile);
                if (!string.Equals(fileName, typeNames[0], StringComparison.Ordinal))
                {
                    failures.Add($"{SourceFileDiscovery.RelativePath(repositoryRoot, sourceFile)} declares '{typeNames[0]}' but the file name is '{fileName}'.");
                }
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
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

}
