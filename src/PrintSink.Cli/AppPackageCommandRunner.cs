using System.ComponentModel;
using System.Diagnostics;

namespace PrintSink.Cli;

internal static class AppPackageCommandRunner
{
    private const string AppExecutionAlias = "printsink-app.exe";
    private static readonly string HeadlessLogPath = Path.Combine(Path.GetTempPath(), "PrintSink.App.headless.log");

    internal static async Task<int> RunAsync(
        string argument,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        ProcessStartInfo startInfo = new(AppExecutionAlias)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(argument);

        try
        {
            File.Delete(HeadlessLogPath);
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start {AppExecutionAlias}.");
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            string standardOutput = await outputTask.ConfigureAwait(false);
            string standardError = await errorTask.ConfigureAwait(false);

            WriteIfNotEmpty(output, standardOutput);
            WriteIfNotEmpty(error, standardError);
            if (process.ExitCode != CliExitCodes.Success)
            {
                WriteHeadlessLog(error);
            }

            return process.ExitCode;
        }
        catch (Win32Exception ex)
        {
            error.WriteLine($"Unable to start {AppExecutionAlias}: {ex.Message}");
            WritePackageInstallHint(error);
            return CliExitCodes.ValidationFailed;
        }
    }

    private static void WritePackageInstallHint(TextWriter error)
    {
        string? packagePath = FindLatestMsixPackage();
        if (packagePath is null)
        {
            error.WriteLine("Build a signed MSIX package for PrintSink.App, install it with Add-AppxPackage, then retry.");
            return;
        }

        string certificatePath = Path.ChangeExtension(packagePath, ".cer");
        if (File.Exists(certificatePath))
        {
            error.WriteLine("Trust the test certificate if this is the first local install:");
            error.WriteLine($"  Import-Certificate -FilePath \"{certificatePath}\" -CertStoreLocation Cert:\\CurrentUser\\TrustedPeople");
        }

        error.WriteLine("Install the signed MSIX package, then retry:");
        error.WriteLine($"  Add-AppxPackage -Path \"{packagePath}\" -ForceApplicationShutdown -ForceUpdateFromAnyVersion");
    }

    private static string? FindLatestMsixPackage()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            string[] packageDirectories =
            [
                Path.Combine(directory.FullName, "src", "PrintSink.App", "artifacts", "appxpackages"),
                Path.Combine(directory.FullName, "src", "PrintSink.App", "AppPackages"),
            ];

            foreach (string packagesDirectory in packageDirectories)
            {
                if (Directory.Exists(packagesDirectory))
                {
                    string? packagePath = Directory
                        .EnumerateFiles(packagesDirectory, "*.msix", SearchOption.AllDirectories)
                        .Select(path => new FileInfo(path))
                        .OrderByDescending(file => file.LastWriteTimeUtc)
                        .Select(file => file.FullName)
                        .FirstOrDefault();
                    if (packagePath is not null)
                    {
                        return packagePath;
                    }
                }
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static void WriteIfNotEmpty(TextWriter writer, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            writer.Write(text);
        }
    }

    private static void WriteHeadlessLog(TextWriter error)
    {
        if (!File.Exists(HeadlessLogPath))
        {
            error.WriteLine($"No headless diagnostic log was written at {HeadlessLogPath}.");
            return;
        }

        error.WriteLine($"Headless diagnostic log ({HeadlessLogPath}):");
        error.Write(File.ReadAllText(HeadlessLogPath));
    }
}
