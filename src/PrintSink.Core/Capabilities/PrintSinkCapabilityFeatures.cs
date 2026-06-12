namespace PrintSink.Core.Capabilities;

/// <summary>
/// Defines the built-in custom Print Device Capabilities options that PrintSink adds to each queue.
/// </summary>
public static class PrintSinkCapabilityFeatures
{
    /// <summary>
    /// Gets the built-in custom features used by PrintSink virtual printers.
    /// </summary>
    public static IReadOnlyList<CustomFeature> BuiltIn { get; } = Array.AsReadOnly(
        new CustomFeature[]
        {
            new(
                PrintSchemaQualifiedName.Keyword("PageMediaSize"),
                [
                    new CustomFeatureOption(
                        PrintSchemaQualifiedName.PrintSink("Receipt80Millimeter"),
                        false,
                        [
                            new PrintSchemaProperty(PrintSchemaQualifiedName.Keyword("MediaSizeWidth"), PrintSchemaPropertyKind.ScoredProperty, "80000", "xsd:integer"),
                            new PrintSchemaProperty(PrintSchemaQualifiedName.Keyword("MediaSizeHeight"), PrintSchemaPropertyKind.ScoredProperty, "200000", "xsd:integer"),
                            new PrintSchemaProperty(PrintSchemaQualifiedName.Keyword12("PortraitImageableSize"), PrintSchemaPropertyKind.Property, "0,0,80000,200000", "psf2:ImageableAreaType"),
                        ]),
                ]),
            new(
                PrintSchemaQualifiedName.Keyword("PageResolution"),
                [
                    new CustomFeatureOption(
                        PrintSchemaQualifiedName.PrintSink("Dpi600"),
                        true,
                        [
                            new PrintSchemaProperty(PrintSchemaQualifiedName.Keyword("ResolutionX"), PrintSchemaPropertyKind.ScoredProperty, "600", "xsd:integer"),
                            new PrintSchemaProperty(PrintSchemaQualifiedName.Keyword("ResolutionY"), PrintSchemaPropertyKind.ScoredProperty, "600", "xsd:integer"),
                        ]),
                    new CustomFeatureOption(
                        PrintSchemaQualifiedName.PrintSink("Dpi1200"),
                        false,
                        [
                            new PrintSchemaProperty(PrintSchemaQualifiedName.Keyword("ResolutionX"), PrintSchemaPropertyKind.ScoredProperty, "1200", "xsd:integer"),
                            new PrintSchemaProperty(PrintSchemaQualifiedName.Keyword("ResolutionY"), PrintSchemaPropertyKind.ScoredProperty, "1200", "xsd:integer"),
                        ]),
                ]),
            new(
                PrintSchemaQualifiedName.Keyword("PageMediaType"),
                [
                    new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("ArchivePaper"), false, []),
                    new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("ThermalReceiptMedia"), false, []),
                ]),
            new(
                PrintSchemaQualifiedName.Keyword("JobInputBin"),
                [
                    new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("AutomationInputBin"), false, []),
                ]),
            new(
                PrintSchemaQualifiedName.Keyword("JobOutputBin"),
                [
                    new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("AutomationOutputBin"), false, []),
                ]),
            new(
                PrintSchemaQualifiedName.Keyword("JobStapleAllDocuments"),
                [
                    new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("StapleUpperLeft"), false, []),
                ]),
            new(
                PrintSchemaQualifiedName.Keyword("JobPageOrder"),
                [
                    new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("OddPagesThenEvenPages"), false, []),
                ]),
            new(
                PrintSchemaQualifiedName.PrintSink("WatermarkMode"),
                [
                    new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("WatermarkOff"), true, []),
                    new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("WatermarkText"), false, []),
                    new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("WatermarkImage"), false, []),
                ]),
        });
}
