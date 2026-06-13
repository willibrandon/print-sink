using PrintSink.Cli.Tests.Commands;
using PrintSink.Cli.Tests.Tui;
using System.Runtime.CompilerServices;

namespace PrintSink.Cli.Tests;

internal static class MSTestDiscoveryRoots
{
    [ModuleInitializer]
    internal static void PreserveInternalTestTypes()
    {
        _ = new CliApplicationTests();
        _ = new PackageAssetValidationTests();
        _ = new TuiDashboardTests();
    }
}
