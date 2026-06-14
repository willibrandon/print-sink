using System.Runtime.CompilerServices;

namespace PrintSink.Architecture.Tests;

internal static class MSTestDiscoveryRoots
{
    [ModuleInitializer]
    internal static void PreserveInternalTestTypes()
    {
        _ = new BuildScriptContractTests();
        _ = new ContinuousIntegrationContractTests();
        _ = new FeatureEvidenceContractTests();
        _ = new LineEndingPolicyTests();
        _ = new NamespaceStructureTests();
        _ = new NativeXpsContractTests();
        _ = new OneTypePerFileTests();
        _ = new PackageManagementContractTests();
        _ = new PackagingContractTests();
        _ = new ReactorUiContractTests();
        _ = new WarningConfigurationTests();
        _ = new XmlDocumentationTests();
    }
}
