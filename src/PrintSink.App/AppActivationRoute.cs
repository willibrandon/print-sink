using Windows.Graphics.Printing.PrintSupport;
using Windows.Graphics.Printing.Workflow;

namespace PrintSink.App;

internal sealed class AppActivationRoute
{
    private AppActivationRoute(
        long activationId,
        AppActivationRouteKind kind,
        string title,
        string subtitle,
        string? winRtSourceText,
        PrintSupportSettingsActivatedEventArgs? settingsArgs,
        PrintWorkflowJobActivatedEventArgs? jobArgs)
    {
        ActivationId = activationId;
        Kind = kind;
        Title = title;
        Subtitle = subtitle;
        WinRtSourceText = winRtSourceText;
        SettingsArgs = settingsArgs;
        JobArgs = jobArgs;
    }

    internal long ActivationId { get; }

    internal AppActivationRouteKind Kind { get; }

    internal string Title { get; }

    internal string Subtitle { get; }

    internal string? WinRtSourceText { get; }

    internal PrintSupportSettingsActivatedEventArgs? SettingsArgs { get; }

    internal PrintWorkflowJobActivatedEventArgs? JobArgs { get; }

    internal static AppActivationRoute Management(long activationId)
    {
        return new AppActivationRoute(
            activationId,
            AppActivationRouteKind.Management,
            "PrintSink",
            "Virtual printer management",
            null,
            null,
            null);
    }

    internal static AppActivationRoute Settings(
        long activationId,
        PrintSupportSettingsActivatedEventArgs settingsArgs)
    {
        return new AppActivationRoute(
            activationId,
            AppActivationRouteKind.Settings,
            "Print preferences",
            "Print Support Settings UI",
            null,
            settingsArgs,
            null);
    }

    internal static AppActivationRoute JobPreview(
        long activationId,
        PrintWorkflowJobActivatedEventArgs jobArgs)
    {
        return new AppActivationRoute(
            activationId,
            AppActivationRouteKind.JobPreview,
            "Job preview",
            "Print Support Job UI",
            null,
            null,
            jobArgs);
    }

    internal static AppActivationRoute WinRtPrintSource(long activationId, string sourceText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceText);

        return new AppActivationRoute(
            activationId,
            AppActivationRouteKind.WinRtPrintSource,
            "WinRT print source",
            "Windows print pipeline",
            sourceText,
            null,
            null);
    }
}
