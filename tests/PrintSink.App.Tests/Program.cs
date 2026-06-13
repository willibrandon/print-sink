using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PrintSink.App.Tests;

internal static class Program
{
    private static UnitTestApp? app;

    [STAThread]
    private static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            DispatcherQueueSynchronizationContext context =
                new(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            app = new UnitTestApp();
        });
    }
}
