using Microsoft.UI.Reactor;

namespace PrintSink.App;

internal static class UiDispatch
{
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
