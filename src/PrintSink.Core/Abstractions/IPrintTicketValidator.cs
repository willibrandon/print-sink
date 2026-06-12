namespace PrintSink.Abstractions;

/// <summary>
/// Validates and resolves print ticket XML without depending on live WinRT activation objects.
/// </summary>
public interface IPrintTicketValidator
{
    /// <summary>
    /// Validates and resolves a print ticket.
    /// </summary>
    /// <param name="printTicketXml">The print ticket XML.</param>
    /// <returns>The validation result.</returns>
    PrintTicketValidationResult Validate(string printTicketXml);
}
