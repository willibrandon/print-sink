using Hex1b;
using PrintSink.Core.Abstractions;
using PrintSink.Core.Diagnostics;

namespace PrintSink.Cli.Tui;

internal sealed class TuiDashboardRuntimeState
{
    private readonly CancellationToken cancellationToken;
    private readonly Func<string, CancellationToken, Task<TuiPackageCommandResult>> runPackageCommand;
    private readonly Func<PrinterQueueSnapshot> readInstalledQueues;
    private readonly string workingDirectory;
    private Hex1bApp? app;
    private bool isBusy;

    private TuiDashboardRuntimeState(
        string workingDirectory,
        TuiDashboardModel model,
        Func<PrinterQueueSnapshot> readInstalledQueues,
        Func<string, CancellationToken, Task<TuiPackageCommandResult>> runPackageCommand,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(readInstalledQueues);
        ArgumentNullException.ThrowIfNull(runPackageCommand);

        this.workingDirectory = workingDirectory;
        this.readInstalledQueues = readInstalledQueues;
        this.runPackageCommand = runPackageCommand;
        this.cancellationToken = cancellationToken;
        Model = model;
    }

    internal TuiDashboardModel Model { get; private set; }

    internal string Status { get; private set; } = "Ready.";

    internal static async Task<TuiDashboardRuntimeState> CreateAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        return await CreateAsync(
                workingDirectory,
                InstalledPrinterReader.Read,
                RunPackageCommandAsync,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task<TuiDashboardRuntimeState> CreateAsync(
        string workingDirectory,
        Func<PrinterQueueSnapshot> readInstalledQueues,
        Func<string, CancellationToken, Task<TuiPackageCommandResult>> runPackageCommand,
        CancellationToken cancellationToken)
    {
        TuiDashboardModel model = await TuiDashboardModel
            .LoadAsync(
                workingDirectory,
                readInstalledQueues,
                cancellationToken)
            .ConfigureAwait(false);

        return new TuiDashboardRuntimeState(
            workingDirectory,
            model,
            readInstalledQueues,
            runPackageCommand,
            cancellationToken);
    }

    internal void Attach(Hex1bApp hex1bApp)
    {
        app = hex1bApp;
    }

    internal void Refresh()
    {
        StartBackgroundWork("Refreshing dashboard.", RefreshAsync);
    }

    internal void InstallQueues()
    {
        StartBackgroundWork(
            "Installing queues.",
            () => RunQueueCommandAsync("--install-virtual-printers", "Queue install completed."));
    }

    internal void RemoveQueues()
    {
        StartBackgroundWork(
            "Removing queues.",
            () => RunQueueCommandAsync("--remove-virtual-printers", "Queue removal completed."));
    }

    internal void RunSinkTests()
    {
        StartBackgroundWork("Running fixture sink tests.", RunSinkTestsAsync);
    }

    private async Task RefreshAsync()
    {
        try
        {
            Model = await LoadModelAsync().ConfigureAwait(false);
            Status = "Dashboard refreshed.";
        }
        catch (OperationCanceledException)
        {
            Status = "Refresh canceled.";
        }
        catch (IOException exception)
        {
            Status = $"Refresh failed: {exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            Status = $"Refresh failed: {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            Status = $"Refresh failed: {exception.Message}";
        }
        finally
        {
            isBusy = false;
            app?.Invalidate();
        }
    }

    private async Task RunQueueCommandAsync(string packageArgument, string successStatus)
    {
        try
        {
            TuiPackageCommandResult result = await runPackageCommand(packageArgument, cancellationToken)
                .ConfigureAwait(false);
            Model = await LoadModelAsync().ConfigureAwait(false);
            Status = result.ExitCode == CliExitCodes.Success
                ? successStatus
                : $"Queue command failed with exit code {result.ExitCode}: {GetPackageCommandMessage(result)}";
        }
        catch (OperationCanceledException)
        {
            Status = "Queue command canceled.";
        }
        catch (IOException exception)
        {
            Status = $"Queue command failed: {exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            Status = $"Queue command failed: {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            Status = $"Queue command failed: {exception.Message}";
        }
        finally
        {
            isBusy = false;
            app?.Invalidate();
        }
    }

    private async Task RunSinkTestsAsync()
    {
        try
        {
            Model = await LoadModelAsync().ConfigureAwait(false);
            int failedChecks = Model.RouteChecks.Count(routeCheck =>
                routeCheck.Status != VirtualPrinterJobStatus.Succeeded ||
                routeCheck.OutputBytes <= 0);
            Status = failedChecks == 0
                ? $"Fixture sink tests passed for {Model.RouteChecks.Count} endpoints."
                : $"Fixture sink tests failed for {failedChecks} endpoints.";
        }
        catch (OperationCanceledException)
        {
            Status = "Fixture sink tests canceled.";
        }
        catch (IOException exception)
        {
            Status = $"Fixture sink tests failed: {exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            Status = $"Fixture sink tests failed: {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            Status = $"Fixture sink tests failed: {exception.Message}";
        }
        finally
        {
            isBusy = false;
            app?.Invalidate();
        }
    }

    private void StartBackgroundWork(string startingStatus, Func<Task> work)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        Status = startingStatus;
        app?.Invalidate();
        _ = work();
    }

    private Task<TuiDashboardModel> LoadModelAsync()
    {
        return TuiDashboardModel.LoadAsync(
            workingDirectory,
            readInstalledQueues,
            cancellationToken);
    }

    private static async Task<TuiPackageCommandResult> RunPackageCommandAsync(
        string argument,
        CancellationToken cancellationToken)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exitCode = await AppPackageCommandRunner
            .RunAsync(argument, output, error, cancellationToken)
            .ConfigureAwait(false);
        return new TuiPackageCommandResult(exitCode, output.ToString(), error.ToString());
    }

    private static string GetPackageCommandMessage(TuiPackageCommandResult result)
    {
        string message = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
        string firstLine = message
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "No diagnostic output.";
        return firstLine;
    }
}
