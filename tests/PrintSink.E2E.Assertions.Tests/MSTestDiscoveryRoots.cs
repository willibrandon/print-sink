using System.Runtime.CompilerServices;

namespace PrintSink.E2E.Assertions.Tests;

internal static class MSTestDiscoveryRoots
{
    [ModuleInitializer]
    internal static void PreserveInternalTestTypes()
    {
        _ = new DocumentAssertionsTests();
    }
}
