using Windows.ApplicationModel.Background;

namespace PrintSink.Tasks;

/// <summary>
/// Receives virtual printer workflow activations for PrintSink software printer queues.
/// </summary>
public sealed class VirtualPrinterBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// Runs the virtual printer background task activation.
    /// </summary>
    /// <param name="taskInstance">The background task instance supplied by the print system.</param>
    public void Run(IBackgroundTaskInstance taskInstance)
    {
        ArgumentNullException.ThrowIfNull(taskInstance);

        BackgroundTaskDeferral deferral = taskInstance.GetDeferral();
        BackgroundTaskExecutionGuard guard = new(taskInstance, deferral);
        guard.Run(ExecuteAsync);
    }

    private static Task ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
