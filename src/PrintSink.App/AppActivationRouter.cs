using Microsoft.Windows.AppLifecycle;
using Windows.Graphics.Printing.PrintSupport;
using Windows.Graphics.Printing.Workflow;

namespace PrintSink.App;

internal static class AppActivationRouter
{
    private static long nextActivationId;

    internal static AppActivationRoute GetCurrentRoute()
    {
        return From(AppInstance.GetCurrent().GetActivatedEventArgs());
    }

    internal static AppActivationRoute From(AppActivationArguments args)
    {
        ArgumentNullException.ThrowIfNull(args);

        long activationId = Interlocked.Increment(ref nextActivationId);
        return args.Kind switch
        {
            ExtendedActivationKind.PrintSupportSettingsUI
                when args.Data is PrintSupportSettingsActivatedEventArgs settingsArgs =>
                    AppActivationRoute.Settings(activationId, settingsArgs),
            ExtendedActivationKind.PrintSupportJobUI
                when args.Data is PrintWorkflowJobActivatedEventArgs jobArgs =>
                    AppActivationRoute.JobPreview(activationId, jobArgs),
            _ => AppActivationRoute.Management(activationId),
        };
    }
}
