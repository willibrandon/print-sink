using Windows.Foundation;
using Windows.Graphics.Printing.Workflow;

namespace PrintSink.App;

internal sealed class JobUiDeferralState
{
    private PrintWorkflowConfiguration? configuration;
    private Deferral? pdlDeferral;
    private Deferral? notificationDeferral;

    internal void SetPdl(PrintWorkflowConfiguration nextConfiguration, Deferral nextDeferral)
    {
        ArgumentNullException.ThrowIfNull(nextConfiguration);
        ArgumentNullException.ThrowIfNull(nextDeferral);

        configuration = nextConfiguration;
        CompletePdl();
        pdlDeferral = nextDeferral;
    }

    internal void SetNotification(Deferral nextDeferral)
    {
        ArgumentNullException.ThrowIfNull(nextDeferral);

        CompleteNotification();
        notificationDeferral = nextDeferral;
    }

    internal void AbortAndComplete()
    {
        configuration?.AbortPrintFlow(PrintWorkflowJobAbortReason.UserCanceled);
        CompleteAll();
    }

    internal void CompleteAll()
    {
        CompletePdl();
        CompleteNotification();
    }

    private void CompletePdl()
    {
        pdlDeferral?.Complete();
        pdlDeferral = null;
    }

    private void CompleteNotification()
    {
        notificationDeferral?.Complete();
        notificationDeferral = null;
    }
}
