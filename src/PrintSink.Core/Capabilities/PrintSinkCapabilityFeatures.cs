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
                PrintSchemaQualifiedName.Keyword("PageResolution"),
                [
                    new CustomFeatureOption(
                        PrintSchemaQualifiedName.PrintSink("Dpi600"),
                        true,
                        [
                            new PrintSchemaProperty(PrintSchemaQualifiedName.Keyword("ResolutionX"), PrintSchemaPropertyKind.ScoredProperty, "600", "xsd:integer"),
                            new PrintSchemaProperty(PrintSchemaQualifiedName.Keyword("ResolutionY"), PrintSchemaPropertyKind.ScoredProperty, "600", "xsd:integer"),
                        ]),
                ]),
            new(
                PrintSchemaQualifiedName.Keyword("PageMediaType"),
                [
                    new CustomFeatureOption(PrintSchemaQualifiedName.PrintSink("ArchivePaper"), false, []),
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
