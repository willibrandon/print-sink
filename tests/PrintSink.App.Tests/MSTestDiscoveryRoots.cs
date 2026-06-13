using System.Runtime.CompilerServices;

namespace PrintSink.App.Tests;

internal static class MSTestDiscoveryRoots
{
    [ModuleInitializer]
    internal static void PreserveInternalTestTypes()
    {
        _ = new AppPackageTests();
    }
}
