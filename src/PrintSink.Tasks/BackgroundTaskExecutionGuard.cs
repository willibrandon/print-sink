using Windows.ApplicationModel.Background;
using PrintSink.Diagnostics;

namespace PrintSink.Tasks;

/// <summary>
/// Runs a background task handler with cancellation tracking and exactly-once deferral completion.
/// </summary>
internal sealed class BackgroundTaskExecutionGuard : IDisposable
{
    private readonly IBackgroundTaskInstance taskInstance;
    private readonly BackgroundTaskDeferral deferral;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private int isCompleted;
    private int isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundTaskExecutionGuard"/> class.
    /// </summary>
    /// <param name="taskInstance">The background task instance.</param>
    /// <param name="deferral">The background task deferral.</param>
    public BackgroundTaskExecutionGuard(IBackgroundTaskInstance taskInstance, BackgroundTaskDeferral deferral)
    {
        ArgumentNullException.ThrowIfNull(taskInstance);
        ArgumentNullException.ThrowIfNull(deferral);

        this.taskInstance = taskInstance;
        this.deferral = deferral;
        taskInstance.Canceled += OnCanceled;
    }

    /// <summary>
    /// Starts the handler and logs any terminal fault.
    /// </summary>
    /// <param name="handler">The task handler.</param>
    public void Run(Func<CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        Task operation = RunAsync(handler);
        _ = operation.ContinueWith(
            static (task, state) => LogFaultedTask((string)state!, task),
            taskInstance.Task.Name,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Runs the handler and completes the deferral when it exits.
    /// </summary>
    /// <param name="handler">The task handler.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RunAsync(Func<CancellationToken, Task> handler)
    {
        try
        {
            await handler(cancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
        }
        finally
        {
            CompleteDeferral();
            Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref isDisposed, 1) == 0)
        {
            taskInstance.Canceled -= OnCanceled;
            cancellationTokenSource.Dispose();
        }
    }

    private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
    {
        if (Volatile.Read(ref isDisposed) == 0)
        {
            cancellationTokenSource.Cancel();
        }
    }

    private void CompleteDeferral()
    {
        if (Interlocked.Exchange(ref isCompleted, 1) == 0)
        {
            deferral.Complete();
        }
    }

    private static void LogFaultedTask(string taskName, Task task)
    {
        Exception exception = task.Exception?.GetBaseException() ?? new InvalidOperationException("The background task failed without an exception.");
        PrintSinkEventSource.Log.BackgroundTaskFailed(
            taskName,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message);
    }
}
