using Windows.ApplicationModel.Background;

namespace PrintSink.Tasks;

/// <summary>
/// Receives shared Print Support Workflow activations for physical-printer parity paths.
/// </summary>
public sealed class PrintSupportWorkflowBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// Runs the print support workflow background task activation.
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
