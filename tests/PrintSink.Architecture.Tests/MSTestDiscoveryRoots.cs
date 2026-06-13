using System.Runtime.CompilerServices;

namespace PrintSink.Architecture.Tests;

internal static class MSTestDiscoveryRoots
{
    [ModuleInitializer]
    internal static void PreserveInternalTestTypes()
    {
        _ = new FeatureEvidenceContractTests();
        _ = new NamespaceStructureTests();
        _ = new OneTypePerFileTests();
        _ = new WarningConfigurationTests();
        _ = new XmlDocumentationTests();
    }
}
