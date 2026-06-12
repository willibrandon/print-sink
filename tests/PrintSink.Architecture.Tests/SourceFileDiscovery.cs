using System.Xml.Linq;

namespace PrintSink.Architecture.Tests;

internal static class SourceFileDiscovery
{
    internal static string FindRepositoryRoot()
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

        throw new DirectoryNotFoundException("Could not locate PrintSink.slnx.");
    }

    internal static string[] EnumerateRepositorySourceFiles(string repositoryRoot)
    {
        return [.. Directory
            .EnumerateFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsRepositorySourceFile)
            .Order(StringComparer.OrdinalIgnoreCase)];
    }

    internal static string RelativePath(string repositoryRoot, string path)
    {
        return Path.GetRelativePath(repositoryRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    internal static string GetExpectedNamespace(string sourceFile)
    {
        string projectFile = FindProjectFile(sourceFile);
        string projectDirectory = Path.GetDirectoryName(projectFile)!;
        string rootNamespace = GetRootNamespace(projectFile);
        string relativeDirectory = Path.GetDirectoryName(Path.GetRelativePath(projectDirectory, sourceFile))
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(relativeDirectory) || relativeDirectory == ".")
        {
            return rootNamespace;
        }

        string namespaceSuffix = relativeDirectory
            .Replace(Path.DirectorySeparatorChar, '.')
            .Replace(Path.AltDirectorySeparatorChar, '.');
        return $"{rootNamespace}.{namespaceSuffix}";
    }

    private static bool IsRepositorySourceFile(string path)
    {
        string normalizedPath = path.Replace(Path.DirectorySeparatorChar, '/');
        bool isSourceRoot = normalizedPath.Contains("/src/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/tests/", StringComparison.OrdinalIgnoreCase);
        return isSourceRoot
            && !normalizedPath.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            && !normalizedPath.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            && !normalizedPath.Contains("/AppPackages/", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindProjectFile(string sourceFile)
    {
        DirectoryInfo? directory = Directory.GetParent(sourceFile);
        while (directory is not null)
        {
            string[] projectFiles = Directory.GetFiles(directory.FullName, "*.csproj", SearchOption.TopDirectoryOnly);
            if (projectFiles.Length == 1)
            {
                return projectFiles[0];
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate a C# project for '{sourceFile}'.");
    }

    private static string GetRootNamespace(string projectFile)
    {
        XDocument document = XDocument.Load(projectFile);
        string? rootNamespace = document
            .Descendants("RootNamespace")
            .Select(element => element.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(rootNamespace))
        {
            return rootNamespace;
        }

        return Path.GetFileNameWithoutExtension(projectFile);
    }
}
