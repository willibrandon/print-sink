namespace PrintSink.Core.Pdl;

/// <summary>
/// Converts PDL streams between formats supported by the print workflow.
/// </summary>
public interface IPdlConverter
{
    /// <summary>
    /// Converts a PDL stream.
    /// </summary>
    /// <param name="source">The source PDL stream.</param>
    /// <param name="conversionKind">The conversion to perform.</param>
    /// <param name="cancellationToken">A token that cancels the conversion.</param>
    /// <returns>A stream containing the converted PDL.</returns>
    Task<Stream> ConvertAsync(Stream source, PdlConversionKind conversionKind, CancellationToken cancellationToken = default);
}
