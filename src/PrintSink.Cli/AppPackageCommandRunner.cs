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

            await WriteIfNotEmptyAsync(output, standardOutput).ConfigureAwait(false);
            await WriteIfNotEmptyAsync(error, standardError).ConfigureAwait(false);
            if (process.ExitCode != CliExitCodes.Success)
            {
                await WriteHeadlessLogAsync(error, cancellationToken).ConfigureAwait(false);
            }

            return process.ExitCode;
        }
        catch (Win32Exception ex)
        {
            await error.WriteLineAsync($"Unable to start {AppExecutionAlias}: {ex.Message}").ConfigureAwait(false);
            await WritePackageInstallHintAsync(error).ConfigureAwait(false);
            return CliExitCodes.ValidationFailed;
        }
    }

    private static async Task WritePackageInstallHintAsync(TextWriter error)
    {
        string? packagePath = FindLatestMsixPackage();
        if (packagePath is null)
        {
            await error.WriteLineAsync("Build a signed MSIX package for PrintSink.App, install it with Add-AppxPackage, then retry.").ConfigureAwait(false);
            return;
        }

        string certificatePath = Path.ChangeExtension(packagePath, ".cer");
        if (File.Exists(certificatePath))
        {
            await error.WriteLineAsync("Trust the test certificate if this is the first local install:").ConfigureAwait(false);
            await error.WriteLineAsync($"  Import-Certificate -FilePath \"{certificatePath}\" -CertStoreLocation Cert:\\CurrentUser\\TrustedPeople").ConfigureAwait(false);
        }

        await error.WriteLineAsync("Install the signed MSIX package, then retry:").ConfigureAwait(false);
        await error.WriteLineAsync($"  Add-AppxPackage -Path \"{packagePath}\" -ForceApplicationShutdown -ForceUpdateFromAnyVersion").ConfigureAwait(false);
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

    private static async Task WriteIfNotEmptyAsync(TextWriter writer, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            await writer.WriteAsync(text).ConfigureAwait(false);
        }
    }

    private static async Task WriteHeadlessLogAsync(TextWriter error, CancellationToken cancellationToken)
    {
        if (!File.Exists(HeadlessLogPath))
        {
            await error.WriteLineAsync($"No headless diagnostic log was written at {HeadlessLogPath}.").ConfigureAwait(false);
            return;
        }

        await error.WriteLineAsync($"Headless diagnostic log ({HeadlessLogPath}):").ConfigureAwait(false);
        await error.WriteAsync(await File.ReadAllTextAsync(HeadlessLogPath, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
    }
}
