namespace PrintSink.Tickets;

/// <summary>
/// Maps print ticket XML into print-stack-neutral IPP job attributes.
/// </summary>
public interface IIppAttributeMapper
{
    /// <summary>
    /// Maps print ticket XML into IPP job attributes.
    /// </summary>
    /// <param name="printTicketXml">The print ticket XML.</param>
    /// <param name="options">The mapping options.</param>
    /// <param name="passwordOptions">Optional encrypted job password options.</param>
    /// <returns>The mapped attributes keyed by IPP attribute name.</returns>
    IReadOnlyDictionary<string, IppAttributeValue> FromPrintTicket(
        string printTicketXml,
        AttributeMergePolicyOptions options,
        JobPasswordOptions? passwordOptions = null);
}
