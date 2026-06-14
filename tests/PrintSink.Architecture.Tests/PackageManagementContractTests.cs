using System.Text.Json;
using System.Xml.Linq;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests package management and test-runner configuration contracts.
/// </summary>
[TestClass]
internal sealed class PackageManagementContractTests
{
    /// <summary>
    /// Verifies package versions stay centralized in Directory.Packages.props.
    /// </summary>
    [TestMethod]
    public void PackageReferencesDoNotDeclareInlineVersions()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string packagesPath = Path.Combine(repositoryRoot, "Directory.Packages.props");
        XDocument packagesDocument = XDocument.Load(packagesPath);
        string? centralPackageManagement = packagesDocument
            .Descendants()
            .Where(static element => element.Name.LocalName == "ManagePackageVersionsCentrally")
            .Select(static element => element.Value.Trim())
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
        HashSet<string> centralPackageIds = packagesDocument
            .Descendants()
            .Where(static element => element.Name.LocalName == "PackageVersion")
            .Select(static element => (string?)element.Attribute("Include"))
            .Where(static packageId => !string.IsNullOrWhiteSpace(packageId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        Assert.AreEqual("true", centralPackageManagement, "Central Package Management must stay enabled.");

        List<string> failures = [];
        foreach (string projectFile in EnumeratePackageReferenceFiles(repositoryRoot))
        {
            XDocument projectDocument = XDocument.Load(projectFile);
            foreach (XElement packageReference in projectDocument.Descendants().Where(static element => element.Name.LocalName == "PackageReference"))
            {
                string packageId = (string?)packageReference.Attribute("Include") ?? "<missing Include>";
                string relativePath = SourceFileDiscovery.RelativePath(repositoryRoot, projectFile);

                if (packageReference.Attribute("Version") is not null)
                {
                    failures.Add($"{relativePath} declares an inline Version for PackageReference '{packageId}'.");
                }

                if (packageReference.Elements().Any(static element => element.Name.LocalName == "Version"))
                {
                    failures.Add($"{relativePath} declares an inline Version element for PackageReference '{packageId}'.");
                }

                if (!centralPackageIds.Contains(packageId))
                {
                    failures.Add($"{relativePath} references package '{packageId}' without a matching PackageVersion.");
                }
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Verifies global.json keeps the SDK and Microsoft.Testing.Platform runner pinned.
    /// </summary>
    [TestMethod]
    public void GlobalJsonPinsDotNetSdkAndMicrosoftTestingPlatform()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string globalJsonPath = Path.Combine(repositoryRoot, "global.json");
        string designPath = Path.Combine(repositoryRoot, "docs", "DESIGN.md");
        string testingPath = Path.Combine(repositoryRoot, "docs", "TESTING.md");

        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(globalJsonPath));
        JsonElement root = globalJson.RootElement;

        Assert.AreEqual("10.0.301", root.GetProperty("sdk").GetProperty("version").GetString());
        Assert.AreEqual("latestFeature", root.GetProperty("sdk").GetProperty("rollForward").GetString());
        Assert.AreEqual("Microsoft.Testing.Platform", root.GetProperty("test").GetProperty("runner").GetString());

        string design = File.ReadAllText(designPath);
        string testing = File.ReadAllText(testingPath);
        Assert.Contains("global.json` opts `dotnet test` into the .NET 10 `Microsoft.Testing.Platform` runner", design);
        Assert.Contains("Microsoft.Testing.Platform runner", testing);
    }

    private static string[] EnumeratePackageReferenceFiles(string repositoryRoot)
    {
        return [.. Directory
            .EnumerateFiles(repositoryRoot, "*.*", SearchOption.AllDirectories)
            .Where(path =>
            {
                string extension = Path.GetExtension(path);
                return extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".vcxproj", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase);
            })
            .Where(path =>
            {
                string normalizedPath = SourceFileDiscovery.RelativePath(repositoryRoot, path);
                return !normalizedPath.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase)
                    && !normalizedPath.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                    && !normalizedPath.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                    && !normalizedPath.Contains("/AppPackages/", StringComparison.OrdinalIgnoreCase);
            })
            .Order(StringComparer.OrdinalIgnoreCase)];
    }
}
