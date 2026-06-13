using System.Xml.Linq;
using PrintSink.Core.Tickets;

namespace PrintSink.Core.Tests.Tickets;

/// <summary>
/// Tests print ticket validation.
/// </summary>
[TestClass]
internal sealed class PrintTicketValidatorTests
{
    /// <summary>
    /// Verifies that a well-formed ticket passes validation.
    /// </summary>
    [TestMethod]
    public void ValidateAcceptsWellFormedTicket()
    {
        XDocument printTicket = CreateValidPrintTicket();

        IReadOnlyList<string> messages = PrintTicketValidator.Validate(printTicket);

        Assert.HasCount(0, messages);
    }

    /// <summary>
    /// Verifies that an empty document is rejected.
    /// </summary>
    [TestMethod]
    public void ValidateRejectsEmptyDocument()
    {
        IReadOnlyList<string> messages = PrintTicketValidator.Validate(new XDocument());

        Assert.Contains("Print ticket document is empty.", messages);
    }

    /// <summary>
    /// Verifies that a non-ticket root is rejected.
    /// </summary>
    [TestMethod]
    public void ValidateRejectsWrongRoot()
    {
        XDocument printTicket = XDocument.Parse("<Document />");

        IReadOnlyList<string> messages = PrintTicketValidator.Validate(printTicket);

        Assert.Contains("Print ticket root element must be PrintTicket.", messages);
    }

    /// <summary>
    /// Verifies that a feature with multiple selected options is rejected.
    /// </summary>
    [TestMethod]
    public void ValidateRejectsMultipleFeatureOptions()
    {
        XDocument printTicket = XDocument.Parse(
            """
            <psf:PrintTicket xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework"
                             xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords">
              <psf:Feature name="psk:PageOutputColor">
                <psf:Option name="psk:Color" />
                <psf:Option name="psk:Monochrome" />
              </psf:Feature>
            </psf:PrintTicket>
            """);

        IReadOnlyList<string> messages = PrintTicketValidator.Validate(printTicket);

        Assert.Contains("Print ticket feature 'PageOutputColor' has more than one selected option.", messages);
    }

    /// <summary>
    /// Verifies that a parameter without a value is rejected.
    /// </summary>
    [TestMethod]
    public void ValidateRejectsParameterWithoutValue()
    {
        XDocument printTicket = XDocument.Parse(
            """
            <psf:PrintTicket xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework"
                             xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords">
              <psf:ParameterInit name="psk:JobCopiesAllDocuments" />
            </psf:PrintTicket>
            """);

        IReadOnlyList<string> messages = PrintTicketValidator.Validate(printTicket);

        Assert.Contains("Print ticket parameter 'JobCopiesAllDocuments' is missing a value.", messages);
    }

    private static XDocument CreateValidPrintTicket()
    {
        return XDocument.Parse(
            """
            <psf:PrintTicket xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework"
                             xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords">
              <psf:Feature name="psk:PageOutputColor">
                <psf:Option name="psk:Color" />
              </psf:Feature>
              <psf:ParameterInit name="psk:JobCopiesAllDocuments">
                <psf:Value>1</psf:Value>
              </psf:ParameterInit>
            </psf:PrintTicket>
            """);
    }
}
