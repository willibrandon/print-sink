using Microsoft.Windows.AppLifecycle;
using System.Text;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;

namespace PrintSink.App;

/// <summary>
/// Handles package-identity commands that should run without showing the WinUI shell.
/// </summary>
internal static class VirtualPrinterCommandLine
{
    private const int Success = 0;
    private const int Failure = 1;

    internal static async Task<int?> RunIfRequestedAsync(
        IReadOnlyList<string> args,
        AppActivationArguments activationArguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(activationArguments);

        List<string> commandArgs = [.. args, .. GetActivationArguments(activationArguments)];
        bool install = Contains(commandArgs, "--install-virtual-printers");
        bool remove = Contains(commandArgs, "--remove-virtual-printers");
        bool enableJobUi = Contains(commandArgs, "--enable-job-ui");
        bool disableJobUi = Contains(commandArgs, "--disable-job-ui");
        bool help = Contains(commandArgs, "--help") || Contains(commandArgs, "-h") || Contains(commandArgs, "-?");
        if (!install && !remove && !enableJobUi && !disableJobUi && !help)
        {
            return null;
        }

        if (help && !install && !remove && !enableJobUi && !disableJobUi)
        {
            WriteHelp();
            SetCommandLineExitCode(activationArguments, Success);
            return Success;
        }

        if ((install && remove) || (enableJobUi && disableJobUi))
        {
            SetCommandLineExitCode(activationArguments, Failure);
            return Failure;
        }

        int exitCode = Failure;
        Deferral? deferral = GetCommandLineDeferral(activationArguments);
        try
        {
            if (enableJobUi || disableJobUi)
            {
                await AppSettingsStoreFactory
                    .Create()
                    .SaveJobUiOptionsAsync(new(enableJobUi), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (install)
            {
                await VirtualPrinterInstaller.InstallAllAsync(cancellationToken).ConfigureAwait(false);
            }

            if (remove)
            {
                await VirtualPrinterInstaller.RemoveAllAsync(cancellationToken).ConfigureAwait(false);
            }

            exitCode = Success;
            return exitCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            WriteDiagnostic(ex.ToString());
            return exitCode;
        }
        finally
        {
            SetCommandLineExitCode(activationArguments, exitCode);
            deferral?.Complete();
        }
    }

    private static string[] GetActivationArguments(AppActivationArguments activationArguments)
    {
        return activationArguments.Data switch
        {
            CommandLineActivatedEventArgs commandLineArgs => SplitArguments(commandLineArgs.Operation.Arguments),
            LaunchActivatedEventArgs launchArgs => SplitArguments(launchArgs.Arguments),
            _ => [],
        };
    }

    internal static string[] SplitArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return [];
        }

        List<string> result = [];
        StringBuilder current = new();
        bool inQuotes = false;
        bool argumentStarted = false;
        int backslashCount = 0;

        foreach (char character in arguments)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                current.Append('\\', backslashCount / 2);
                if (backslashCount % 2 == 0)
                {
                    inQuotes = !inQuotes;
                    argumentStarted = true;
                }
                else
                {
                    current.Append('"');
                    argumentStarted = true;
                }

                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                current.Append('\\', backslashCount);
                backslashCount = 0;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                AddArgument(result, current, ref argumentStarted);
                continue;
            }

            current.Append(character);
            argumentStarted = true;
        }

        if (backslashCount > 0)
        {
            current.Append('\\', backslashCount);
        }

        AddArgument(result, current, ref argumentStarted);
        return [.. result];
    }

    private static void AddArgument(List<string> result, StringBuilder current, ref bool argumentStarted)
    {
        if (!argumentStarted)
        {
            return;
        }

        result.Add(current.ToString());
        current.Clear();
        argumentStarted = false;
    }

    private static void SetCommandLineExitCode(AppActivationArguments activationArguments, int? exitCode)
    {
        if (exitCode is int value && activationArguments.Data is CommandLineActivatedEventArgs commandLineArgs)
        {
            commandLineArgs.Operation.ExitCode = value;
        }
    }

    private static Deferral? GetCommandLineDeferral(AppActivationArguments activationArguments)
    {
        return activationArguments.Data is CommandLineActivatedEventArgs commandLineArgs
            ? commandLineArgs.Operation.GetDeferral()
            : null;
    }

    internal static void WriteDiagnostic(string message)
    {
        string path = Path.Combine(Path.GetTempPath(), "PrintSink.App.headless.log");
        File.AppendAllText(path, $"{DateTimeOffset.Now:u} {message}{Environment.NewLine}");
        System.Diagnostics.Trace.TraceError(message);
    }

    internal static void WriteStartupTrace(string message)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("PRINTSINK_TRACE_STARTUP"), "1", StringComparison.Ordinal))
        {
            WriteDiagnostic(message);
        }
    }

    internal static void WriteHelp()
    {
        string help = string.Join(
            Environment.NewLine,
            "PrintSink packaged app commands:",
            "  --install-virtual-printers  Install PrintSink virtual printer queues.",
            "  --remove-virtual-printers   Remove PrintSink virtual printer queues.",
            "  --disable-job-ui            Process jobs without launching the foreground Job UI.",
            "  --enable-job-ui             Restore foreground Job UI launch behavior.",
            "",
            "For visible operator help, run: dotnet run --project src\\PrintSink.Cli -- --help");
        Console.Out.WriteLine(help);
        WriteDiagnostic(help);
    }

    internal static string Describe(AppActivationArguments activationArguments)
    {
        ArgumentNullException.ThrowIfNull(activationArguments);

        return activationArguments.Data switch
        {
            CommandLineActivatedEventArgs commandLineArgs => $"Activation data: {activationArguments.Data.GetType().FullName}; command line: {commandLineArgs.Operation.Arguments}",
            LaunchActivatedEventArgs launchArgs => $"Activation data: {activationArguments.Data.GetType().FullName}; launch args: {launchArgs.Arguments}",
            null => "Activation data: <null>",
            _ => $"Activation data: {activationArguments.Data.GetType().FullName}",
        };
    }

    private static bool Contains(IReadOnlyList<string> args, string value)
    {
        return args.Any(arg => string.Equals(arg, value, StringComparison.OrdinalIgnoreCase));
    }
}
