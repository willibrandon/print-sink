namespace PrintSink.Core.Abstractions;

/// <summary>
/// Provides access to a print ticket without exposing WinRT event objects to core logic.
/// </summary>
public interface IPrintTicket
{
    /// <summary>
    /// Gets the print ticket XML.
    /// </summary>
    string Xml { get; }
}
