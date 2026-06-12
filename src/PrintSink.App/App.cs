using Microsoft.UI.Reactor;
using Microsoft.Windows.AppLifecycle;
using PrintSink.App;

try
{
    VirtualPrinterCommandLine.WriteStartupTrace($"Process args: {string.Join('|', args)}");
    AppActivationArguments activationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
    VirtualPrinterCommandLine.WriteStartupTrace(VirtualPrinterCommandLine.Describe(activationArguments));

    int? headlessExitCode = await VirtualPrinterCommandLine
        .RunIfRequestedAsync(args, activationArguments, CancellationToken.None)
        .ConfigureAwait(false);
    if (headlessExitCode is int exitCode)
    {
        return exitCode;
    }

    AppActivationState.SetRoute(AppActivationRouter.From(activationArguments));
    AppInstance.GetCurrent().Activated += (_, args) =>
    {
        AppActivationState.SetRoute(AppActivationRouter.From(args));
    };

    ReactorApp.Run<AppRoot>("PrintSink", width: 1040, height: 720);

    return 0;
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    VirtualPrinterCommandLine.WriteDiagnostic($"Startup failed: {ex}");
    return 1;
}
