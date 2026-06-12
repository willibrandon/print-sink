using Hex1b;

namespace PrintSink.Cli.Tui;

internal sealed class TuiDashboardRuntimeState
{
    private readonly CancellationToken cancellationToken;
    private readonly string workingDirectory;
    private Hex1bApp? app;
    private bool isRefreshing;

    private TuiDashboardRuntimeState(
        string workingDirectory,
        TuiDashboardModel model,
        CancellationToken cancellationToken)
    {
        this.workingDirectory = workingDirectory;
        this.cancellationToken = cancellationToken;
        Model = model;
    }

    internal TuiDashboardModel Model { get; private set; }

    internal string Status { get; private set; } = "Ready.";

    internal static async Task<TuiDashboardRuntimeState> CreateAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        TuiDashboardModel model = await TuiDashboardModel
            .LoadAsync(workingDirectory, cancellationToken)
            .ConfigureAwait(false);

        return new TuiDashboardRuntimeState(workingDirectory, model, cancellationToken);
    }

    internal void Attach(Hex1bApp hex1bApp)
    {
        app = hex1bApp;
    }

    internal void Refresh()
    {
        if (isRefreshing)
        {
            return;
        }

        isRefreshing = true;
        Status = "Refreshing dashboard.";
        app?.Invalidate();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            Model = await TuiDashboardModel
                .LoadAsync(workingDirectory, cancellationToken)
                .ConfigureAwait(false);
            Status = "Dashboard refreshed.";
        }
        catch (OperationCanceledException)
        {
            Status = "Refresh canceled.";
        }
        catch (Exception exception)
        {
            Status = $"Refresh failed: {exception.Message}";
        }
        finally
        {
            isRefreshing = false;
            app?.Invalidate();
        }
    }
}
