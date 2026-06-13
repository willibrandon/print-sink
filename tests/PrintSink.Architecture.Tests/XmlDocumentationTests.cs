using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests authored XML documentation coverage.
/// </summary>
[TestClass]
internal sealed class XmlDocumentationTests
{
    /// <summary>
    /// Verifies public authored API is documented, including public members on internal types.
    /// </summary>
    [TestMethod]
    public void PublicAuthoredApiHasXmlDocumentation()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string[] sourceFiles = SourceFileDiscovery.EnumerateRepositorySourceFiles(repositoryRoot);

        List<string> failures = [];
        foreach (string sourceFile in sourceFiles)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile), path: sourceFile);
            CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();

            foreach (TypeDeclarationSyntax typeDeclaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (RequiresTypeDocumentation(typeDeclaration) && !HasXmlDocumentation(typeDeclaration))
                {
                    failures.Add(FormatFailure(repositoryRoot, syntaxTree, typeDeclaration, typeDeclaration.Identifier.ValueText));
                }

                foreach (MemberDeclarationSyntax member in typeDeclaration.Members.Where(RequiresMemberDocumentation))
                {
                    if (!HasXmlDocumentation(member))
                    {
                        failures.Add(FormatFailure(repositoryRoot, syntaxTree, member, GetMemberName(member)));
                    }
                }
            }

            foreach (EnumDeclarationSyntax enumDeclaration in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                if (RequiresTypeDocumentation(enumDeclaration) && !HasXmlDocumentation(enumDeclaration))
                {
                    failures.Add(FormatFailure(repositoryRoot, syntaxTree, enumDeclaration, enumDeclaration.Identifier.ValueText));
                }

                if (RequiresTypeDocumentation(enumDeclaration))
                {
                    foreach (EnumMemberDeclarationSyntax member in enumDeclaration.Members)
                    {
                        if (!HasXmlDocumentation(member))
                        {
                            failures.Add(FormatFailure(repositoryRoot, syntaxTree, member, member.Identifier.ValueText));
                        }
                    }
                }
            }

            foreach (DelegateDeclarationSyntax delegateDeclaration in root.DescendantNodes().OfType<DelegateDeclarationSyntax>())
            {
                if (RequiresDelegateDocumentation(delegateDeclaration) && !HasXmlDocumentation(delegateDeclaration))
                {
                    failures.Add(FormatFailure(repositoryRoot, syntaxTree, delegateDeclaration, delegateDeclaration.Identifier.ValueText));
                }
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    private static bool RequiresTypeDocumentation(TypeDeclarationSyntax declaration)
    {
        return IsPublic(declaration.Modifiers)
            || declaration.Members.Any(RequiresMemberDocumentation);
    }

    private static bool RequiresTypeDocumentation(EnumDeclarationSyntax declaration)
    {
        return IsPublic(declaration.Modifiers);
    }

    private static bool RequiresDelegateDocumentation(DelegateDeclarationSyntax declaration)
    {
        return IsPublic(declaration.Modifiers);
    }

    private static bool RequiresMemberDocumentation(MemberDeclarationSyntax declaration)
    {
        return declaration switch
        {
            BaseTypeDeclarationSyntax => false,
            DelegateDeclarationSyntax => false,
            _ => IsPublic(declaration.Modifiers),
        };
    }

    private static bool IsPublic(SyntaxTokenList modifiers)
    {
        return modifiers.Any(SyntaxKind.PublicKeyword);
    }

    private static bool HasXmlDocumentation(CSharpSyntaxNode node)
    {
        return node.GetLeadingTrivia().Any(static trivia =>
            trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
    }

    private static string FormatFailure(
        string repositoryRoot,
        SyntaxTree syntaxTree,
        CSharpSyntaxNode node,
        string declarationName)
    {
        FileLinePositionSpan lineSpan = syntaxTree.GetLineSpan(node.Span);
        return string.Concat(
            SourceFileDiscovery.RelativePath(repositoryRoot, syntaxTree.FilePath),
            ":",
            lineSpan.StartLinePosition.Line + 1,
            " is missing XML documentation for ",
            declarationName,
            ".");
    }

    private static string GetMemberName(MemberDeclarationSyntax declaration)
    {
        return declaration switch
        {
            ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
            ConversionOperatorDeclarationSyntax conversionOperator => $"operator {conversionOperator.Type}",
            EventDeclarationSyntax eventDeclaration => eventDeclaration.Identifier.ValueText,
            EventFieldDeclarationSyntax eventField => string.Join(",", eventField.Declaration.Variables.Select(static variable => variable.Identifier.ValueText)),
            FieldDeclarationSyntax field => string.Join(",", field.Declaration.Variables.Select(static variable => variable.Identifier.ValueText)),
            IndexerDeclarationSyntax => "this[]",
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            OperatorDeclarationSyntax operatorDeclaration => $"operator {operatorDeclaration.OperatorToken.ValueText}",
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            _ => declaration.Kind().ToString(),
        };
    }
}
