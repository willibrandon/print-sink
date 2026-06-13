using System.Xml.Linq;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests repository warning configuration rules.
/// </summary>
[TestClass]
public sealed class WarningConfigurationTests
{
    /// <summary>
    /// Verifies repository projects do not suppress compiler or analyzer warnings.
    /// </summary>
    [TestMethod]
    public void Project_files_do_not_disable_warnings()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string[] projectFiles = SourceFileDiscovery.EnumerateRepositoryBuildFiles(repositoryRoot);

        List<string> failures = [];
        foreach (string projectFile in projectFiles)
        {
            XDocument document = XDocument.Load(projectFile);
            foreach (XElement noWarnElement in document.Descendants().Where(static element => element.Name.LocalName == "NoWarn"))
            {
                failures.Add(
                    $"{SourceFileDiscovery.RelativePath(repositoryRoot, projectFile)} declares NoWarn='{noWarnElement.Value}'.");
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }
}
