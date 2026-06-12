using Windows.ApplicationModel.Background;

namespace PrintSink.Tasks;

/// <summary>
/// Receives Print Support Extension activations for ticket validation, PDC refresh, and printer-selected UI data.
/// </summary>
public sealed class PrintSupportExtensionBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// Runs the print support extension background task activation.
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
