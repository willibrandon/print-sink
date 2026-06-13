using Microsoft.Windows.AppLifecycle;
using Windows.Graphics.Printing.PrintSupport;
using Windows.Graphics.Printing.Workflow;

namespace PrintSink.App;

internal static class AppActivationRouter
{
    private const string WinRtPrintSourceSwitch = "--winrt-source-print";
    private const string TextOption = "--text";

    private static long nextActivationId;

    internal static AppActivationRoute GetCurrentRoute()
    {
        return From(AppInstance.GetCurrent().GetActivatedEventArgs());
    }

    internal static AppActivationRoute From(AppActivationArguments args)
    {
        return From([], args);
    }

    internal static AppActivationRoute From(IReadOnlyList<string> processArgs, AppActivationArguments args)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(processArgs);

        long activationId = Interlocked.Increment(ref nextActivationId);
        if (TryGetWinRtPrintSourceText(processArgs, args, out string? sourceText))
        {
            return AppActivationRoute.WinRtPrintSource(activationId, sourceText!);
        }

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

    private static bool TryGetWinRtPrintSourceText(
        IReadOnlyList<string> processArgs,
        AppActivationArguments args,
        out string? sourceText)
    {
        List<string> commandArgs = [.. processArgs, .. VirtualPrinterCommandLine.GetActivationArguments(args)];
        if (!Contains(commandArgs, WinRtPrintSourceSwitch))
        {
            sourceText = null;
            return false;
        }

        sourceText = TryGetOptionValue(commandArgs, TextOption) ?? "foo";
        return true;
    }

    private static string? TryGetOptionValue(IReadOnlyList<string> args, string option)
    {
        for (int index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], option, StringComparison.OrdinalIgnoreCase))
            {
                string value = args[index + 1];
                return value.StartsWith("--", StringComparison.Ordinal)
                    ? null
                    : value;
            }
        }

        return null;
    }

    private static bool Contains(IReadOnlyList<string> args, string value)
    {
        return args.Any(arg => string.Equals(arg, value, StringComparison.OrdinalIgnoreCase));
    }
}
