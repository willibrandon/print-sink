using PrintSink.Cli;
using PrintSink.Core.Endpoints;

namespace PrintSink.Cli.Tests.Commands;

/// <summary>
/// Tests validators against the package assets that ship with the app.
/// </summary>
[TestClass]
public sealed class PackageAssetValidationTests
{
    /// <summary>
    /// Verifies the shipped package manifest matches the virtual-printer contract shape.
    /// </summary>
    [TestMethod]
    public void Manifest_lint_accepts_shipped_package_manifest()
    {
        string repositoryRoot = FindRepositoryRoot();
        string manifestPath = Path.Combine(repositoryRoot, "src", "PrintSink.App", "Package.appxmanifest");

        ManifestLintResult result = ManifestLinter.Lint(manifestPath);

        Assert.IsTrue(result.Succeeded, string.Join(Environment.NewLine, result.Messages));
    }

    /// <summary>
    /// Verifies every shipped virtual-printer PDC file has a valid Print Schema shape.
    /// </summary>
    [TestMethod]
    public void Pdc_validate_accepts_shipped_virtual_printer_capabilities()
    {
        string repositoryRoot = FindRepositoryRoot();
        string configDirectory = Path.Combine(repositoryRoot, "src", "PrintSink.App", "Config");
        string[] pdcFiles = [.. Directory
            .EnumerateFiles(configDirectory, "*.pdc.xml", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)];

        Assert.HasCount(EndpointCatalog.All.Count, pdcFiles);

        List<string> failures = [];
        foreach (string pdcFile in pdcFiles)
        {
            ValidationResult result = PdcValidator.Validate(pdcFile);
            if (!result.Succeeded)
            {
                failures.Add($"{Path.GetFileName(pdcFile)}: {string.Join("; ", result.Messages)}");
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
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

        throw new DirectoryNotFoundException("Could not locate PrintSink.slnx.");
    }
}
