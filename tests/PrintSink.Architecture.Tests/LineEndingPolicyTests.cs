using System.Diagnostics;

namespace PrintSink.Architecture.Tests;

/// <summary>
/// Tests repository line-ending policy.
/// </summary>
[TestClass]
internal sealed class LineEndingPolicyTests
{
    /// <summary>
    /// Verifies tracked text files match the line endings declared in .gitattributes.
    /// </summary>
    [TestMethod]
    public void TrackedTextFilesUseDeclaredLineEndings()
    {
        string repositoryRoot = SourceFileDiscovery.FindRepositoryRoot();
        string[] trackedFiles = EnumerateTrackedFiles(repositoryRoot);

        List<string> failures = [];
        foreach (string relativePath in trackedFiles)
        {
            string path = Path.Combine(repositoryRoot, relativePath);
            if (IsBinaryPath(relativePath))
            {
                continue;
            }

            byte[] bytes = File.ReadAllBytes(path);
            int crlfCount = CountLineEndings(bytes, requiresCarriageReturn: true);
            int bareLfCount = CountLineEndings(bytes, requiresCarriageReturn: false);
            if (relativePath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
            {
                if (crlfCount > 0)
                {
                    failures.Add($"{relativePath} contains {crlfCount} CRLF line ending(s); shell scripts must use LF.");
                }
            }
            else if (bareLfCount > 0)
            {
                failures.Add($"{relativePath} contains {bareLfCount} bare LF line ending(s); tracked text files must use CRLF.");
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    private static string[] EnumerateTrackedFiles(string repositoryRoot)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("git", "ls-files -z")
            {
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.AreEqual(0, process.ExitCode, error);
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool IsBinaryPath(string relativePath)
    {
        string extension = Path.GetExtension(relativePath);
        return extension.Equals(".ico", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountLineEndings(byte[] bytes, bool requiresCarriageReturn)
    {
        int count = 0;
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != '\n')
            {
                continue;
            }

            bool hasCarriageReturn = index > 0 && bytes[index - 1] == '\r';
            if (hasCarriageReturn == requiresCarriageReturn)
            {
                count++;
            }
        }

        return count;
    }
}
