namespace PrintSink.Core.Tests;

internal static class MSTestDiscoveryRoots
{
    internal static object[] CreateTestClassInstances() =>
    [
            new Architecture.SourceLayoutTests(),
            new Architecture.ManifestContractTests(),
            new Capabilities.PrintDeviceCapabilitiesEditorTests(),
        new Endpoints.EndpointCatalogTests(),
        new Endpoints.SinkTests(),
        new Pdl.PdlRouterTests(),
        new Processing.VirtualPrinterJobProcessorTests(),
        new Settings.WatermarkSettingsServiceTests(),
        new Tickets.IppAttributeMapperTests(),
    ];
}
