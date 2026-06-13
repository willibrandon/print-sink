using PrintSink.Core.Tests.Capabilities;
using PrintSink.Core.Tests.Diagnostics;
using PrintSink.Core.Tests.Endpoints;
using PrintSink.Core.Tests.Pdl;
using PrintSink.Core.Tests.Processing;
using PrintSink.Core.Tests.Settings;
using PrintSink.Core.Tests.Tickets;
using PrintSink.Core.Tests.Watermark;
using System.Runtime.CompilerServices;

namespace PrintSink.Core.Tests;

internal static class MSTestDiscoveryRoots
{
    [ModuleInitializer]
    internal static void PreserveInternalTestTypes()
    {
        _ = new EndpointCatalogTests();
        _ = new ImageWatermarkTests();
        _ = new IppAttributeMapperTests();
        _ = new JobPasswordOptionsTests();
        _ = new LocalDiagnosticEventStoreTests();
        _ = new LocalSettingsStoreTests();
        _ = new PdlFormatInfoTests();
        _ = new PdlRouterTests();
        _ = new PrintDeviceCapabilitiesEditorTests();
        _ = new PrintDeviceCapabilitiesValidatorTests();
        _ = new PrintDeviceResourcesEditorTests();
        _ = new PrinterDocumentFormatSelectorTests();
        _ = new PrintSinkDiagnosticsTests();
        _ = new PrintTicketValidatorTests();
        _ = new TextWatermarkTests();
        _ = new VirtualPrinterJobProcessorTests();
        _ = new WatermarkOptionsTests();
        _ = new XpsWatermarkPdlTransformerTests();
    }
}
