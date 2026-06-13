using System.Runtime.CompilerServices;

namespace PrintSink.Xps.Tests;

internal static class MSTestDiscoveryRoots
{
    [ModuleInitializer]
    internal static void PreserveInternalTestTypes()
    {
        _ = new NativeXpsPageWatermarkerTests();
    }
}
