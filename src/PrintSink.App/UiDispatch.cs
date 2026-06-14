using Microsoft.UI.Reactor;

namespace PrintSink.App;

internal static class UiDispatch
{
    internal static void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Microsoft.UI.Dispatching.DispatcherQueue? dispatcher = ReactorApp.UIDispatcher;
        if (dispatcher is null)
        {
            _ = Task.Run(action);
            return;
        }

        dispatcher.TryEnqueue(() => action());
    }

    internal static void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Microsoft.UI.Dispatching.DispatcherQueue? dispatcher = ReactorApp.UIDispatcher;
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        dispatcher.TryEnqueue(() => action());
    }
}
