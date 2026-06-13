using Windows.ApplicationModel.Background;

namespace PrintSink.Tasks;

internal sealed class BackgroundTaskHandlerState
{
    private readonly Lock gate = new();
    private BackgroundTaskDeferral? taskDeferral;
    private bool isCancellationRequested;
    private int activeHandlerCount;
    private bool completed;

    internal void Attach(IBackgroundTaskInstance taskInstance)
    {
        ArgumentNullException.ThrowIfNull(taskInstance);

        taskDeferral = taskInstance.GetDeferral();
        taskInstance.Canceled += OnTaskCanceled;
    }

    internal void CompleteWhenIdle()
    {
        lock (gate)
        {
            isCancellationRequested = true;
        }

        CompleteIfIdle();
    }

    internal bool Run(Action action)
    {
        return Run(action, null);
    }

    internal bool Run(Action action, Action<Exception>? onException)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!TryEnterHandler())
        {
            return false;
        }

        try
        {
            action();
            return true;
        }
        catch (Exception exception)
        {
            onException?.Invoke(exception);
            // In-process print background tasks must not let IPC teardown or handler
            // failures escape into the app process.
            return false;
        }
        finally
        {
            ExitHandler();
        }
    }

    private bool TryEnterHandler()
    {
        lock (gate)
        {
            if (isCancellationRequested || completed)
            {
                return false;
            }

            activeHandlerCount++;
            return true;
        }
    }

    private void ExitHandler()
    {
        lock (gate)
        {
            activeHandlerCount--;
        }

        CompleteIfIdle();
    }

    private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
    {
        lock (gate)
        {
            isCancellationRequested = true;
        }

        CompleteIfIdle();
    }

    private void CompleteIfIdle()
    {
        BackgroundTaskDeferral? deferralToComplete = null;
        lock (gate)
        {
            if (!completed && isCancellationRequested && activeHandlerCount == 0)
            {
                completed = true;
                deferralToComplete = taskDeferral;
                taskDeferral = null;
            }
        }

        deferralToComplete?.Complete();
    }
}
