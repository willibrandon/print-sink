namespace PrintSink.Pdl;

/// <summary>
/// Converts page description language streams through a print-stack adapter.
/// </summary>
public interface IPdlConverter
{
    /// <summary>
    /// Converts PDL content into the requested target format.
    /// </summary>
    /// <param name="conversion">The conversion operation to perform.</param>
    /// <param name="printTicketXml">The effective print ticket XML for the job.</param>
    /// <param name="source">The source stream positioned at the beginning.</param>
    /// <param name="target">The target stream to receive converted content.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when conversion finishes.</returns>
    Task ConvertAsync(
        PdlConversionKind conversion,
        string printTicketXml,
        Stream source,
        Stream target,
        CancellationToken cancellationToken = default);
}
