using Microsoft.Windows.AppLifecycle;
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
        if (!install && !remove)
        {
            return null;
        }

        if (install && remove)
        {
            SetCommandLineExitCode(activationArguments, Failure);
            return Failure;
        }

        int exitCode = Failure;
        Deferral? deferral = GetCommandLineDeferral(activationArguments);
        try
        {
            if (install)
            {
                await VirtualPrinterInstaller.InstallAllAsync(cancellationToken).ConfigureAwait(false);
            }
            else
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

    private static string[] SplitArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return [];
        }

        return arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
