using PrintSink.Core.Abstractions;

namespace PrintSink.Cli;

/// <summary>
/// Provides a minimal print-ticket fixture for CLI sink tests.
/// </summary>
internal sealed class FixturePrintTicket : IPrintTicket
{
    /// <inheritdoc />
    public string Xml => "<PrintTicket />";
}
