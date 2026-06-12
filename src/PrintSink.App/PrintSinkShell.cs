using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

namespace PrintSink;

/// <summary>
/// Renders the PrintSink foreground shell.
/// </summary>
internal sealed class PrintSinkShell : Component
{
    private const double DashboardMaxWidth = 1120;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrintSinkShell"/> class.
    /// </summary>
    public PrintSinkShell()
    {
    }

    /// <inheritdoc />
    public override Element Render()
    {
        return FlexColumn(
            TitleBar("PrintSink")
                .Subtitle("Virtual printer management")
                .Icon("ms-appx:///Assets/Square44x44Logo.png")
                .Flex(shrink: 0),
            ScrollView(
                (FlexColumn(
                    Header(),
                    MetricsRow(),
                    MainPanels())
                    with
                    {
                        RowGap = 16,
                    })
                    .FlexPadding(24, 20, 24, 28)
                    .MaxWidth(DashboardMaxWidth)
                    .HAlign(HorizontalAlignment.Center))
                .Flex(grow: 1, basis: 0))
            .Backdrop(BackdropKind.Mica);
    }

    private static StackElement Header()
    {
        return VStack(
            Heading("PrintSink"),
            TextBlock("A packaged virtual printer that receives jobs from Windows and routes them to file, cloud, or custom sinks.")
                .Foreground(Theme.SecondaryText)
                .TextWrapping())
            .Spacing(6);
    }

    private static FlexElement MetricsRow()
    {
        return FlexRow(
            StatCard("Queues", "5", "PDF, XPS, PS, Cloud, PWG").Flex(grow: 1, basis: 0, minWidth: 210),
            StatCard("Package", "MSIX", "Print Support App contracts").Flex(grow: 1, basis: 0, minWidth: 210),
            StatCard("Input", "OXPS / PS", "PDF passthrough where supported").Flex(grow: 1, basis: 0, minWidth: 210),
            StatCard("Runner", "MTP", "MSTest on Microsoft.Testing.Platform").Flex(grow: 1, basis: 0, minWidth: 210))
            with
            {
                ColumnGap = 12,
                RowGap = 12,
                Wrap = FlexWrap.Wrap,
            };
    }

    private static FlexElement MainPanels()
    {
        return FlexRow(
            EndpointPanel().Flex(grow: 1.2, basis: 0, minWidth: 480),
            (FlexColumn(
                PipelinePanel().Flex(grow: 1),
                ValidationPanel().Flex(grow: 1),
                DiagnosticsPanel().Flex(grow: 1))
                with
                {
                    RowGap = 12,
                })
                .Flex(grow: 1, basis: 0, minWidth: 400))
            with
            {
                ColumnGap = 12,
                RowGap = 12,
                Wrap = FlexWrap.Wrap,
                AlignItems = FlexAlign.Stretch,
            };
    }

    private static BorderElement StatCard(string label, string value, string detail)
    {
        return Card(
            VStack(
                Caption(label)
                    .Foreground(Theme.SecondaryText),
                TextBlock(value)
                    .FontSize(24)
                    .SemiBold(),
                Caption(detail)
                    .Foreground(Theme.SecondaryText)
                    .TextWrapping())
                .Spacing(4));
    }

    private static BorderElement EndpointPanel()
    {
        return Card(
            VStack(
                SubHeading("Queues"),
                EndpointRow("PrintSink - PDF", "Save-As PDF", "Ready"),
                EndpointRow("PrintSink - XPS", "OXPS passthrough", "Ready"),
                EndpointRow("PrintSink - PostScript", "PostScript sink", "Ready"),
                EndpointRow("PrintSink - Cloud", "No Save-As target", "Planned"),
                EndpointRow("PrintSink - PWG Raster", "Converter path", "Planned"))
                .Spacing(10));
    }

    private static BorderElement PipelinePanel()
    {
        return Card(
            VStack(
                SubHeading("Pipeline"),
                PipelineStep("1", "Receive PDL", "Virtual-printer workflow activation gets the source stream and print ticket."),
                PipelineStep("2", "Transform", "Core routes passthrough, conversion, and watermark work."),
                PipelineStep("3", "Write sink", "File and cloud sinks use the same job processor contract."))
                .Spacing(12));
    }

    private static BorderElement ValidationPanel()
    {
        return Card(
            VStack(
                SubHeading("Validation"),
                TextBlock("The core library owns format routing, capability edits, ticket mapping, settings, and sink writes so those paths stay testable without a live spooler.")
                    .Foreground(Theme.SecondaryText)
                    .TextWrapping(),
                HStack(
                    Button("Refresh queues").ToolTip("Refresh installed PrintSink queues").AutomationName("Refresh queues"),
                    Button("Open diagnostics").ToolTip("Open job and event diagnostics").AutomationName("Open diagnostics").SubtleButton()))
                .Spacing(12))
            .Flex(grow: 1);
    }

    private static BorderElement DiagnosticsPanel()
    {
        return Card(
            VStack(
                SubHeading("Diagnostics"),
                (FlexRow(
                    DetailRow("Event source", "PrintSink-Diagnostics").Flex(grow: 1, basis: 0, minWidth: 180),
                    DetailRow("Package identity", "Required for PSA contracts").Flex(grow: 1, basis: 0, minWidth: 180),
                    DetailRow("Settings", "Local app data").Flex(grow: 1, basis: 0, minWidth: 180),
                    DetailRow("Core tests", "25 passing").Flex(grow: 1, basis: 0, minWidth: 180))
                    with
                    {
                        ColumnGap = 10,
                        RowGap = 10,
                        Wrap = FlexWrap.Wrap,
                    })
                .Flex(grow: 1))
                .Spacing(12));
    }

    private static BorderElement EndpointRow(string name, string detail, string status)
    {
        return Border(
            FlexRow(
                VStack(
                    TextBlock(name).SemiBold(),
                    Caption(detail)
                        .Foreground(Theme.SecondaryText))
                    .Spacing(2)
                    .Flex(grow: 1, basis: 0),
                StatusBadge(status))
                with
                {
                    ColumnGap = 12,
                    AlignItems = FlexAlign.Center,
                })
            .Padding(12, 8)
            .CornerRadius(6)
            .Background(Theme.LayerFill);
    }

    private static FlexElement PipelineStep(string ordinal, string title, string detail)
    {
        return FlexRow(
            Border(TextBlock(ordinal).SemiBold())
                .Width(32)
                .Height(32)
                .CornerRadius(16)
                .Background(Theme.ControlFillSecondary),
            VStack(
                TextBlock(title).SemiBold(),
                Caption(detail)
                    .Foreground(Theme.SecondaryText)
                    .TextWrapping())
                .Spacing(2)
                .Flex(grow: 1, basis: 0))
            with
            {
                ColumnGap = 10,
                AlignItems = FlexAlign.FlexStart,
            };
    }

    private static BorderElement DetailRow(string label, string value)
    {
        return Border(
            VStack(
                Caption(label).Foreground(Theme.SecondaryText),
                TextBlock(value)
                    .SemiBold()
                    .TextWrapping())
                .Spacing(2))
            .Padding(10, 7)
            .CornerRadius(6)
            .Background(Theme.LayerFill);
    }

    private static BorderElement StatusBadge(string status)
    {
        ThemeRef background = status == "Ready" ? Theme.SystemSuccessBackground : Theme.SystemCautionBackground;
        ThemeRef foreground = status == "Ready" ? Theme.SystemSuccess : Theme.SystemCaution;

        return Border(Caption(status).Foreground(foreground))
            .Padding(10, 4)
            .CornerRadius(10)
            .Background(background);
    }

    private static BorderElement Card(Element child)
    {
        return Border(child)
            .Padding(18)
            .CornerRadius(8)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke);
    }
}
