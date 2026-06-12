using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using static Microsoft.UI.Reactor.Factories;

namespace PrintSink.App;

/// <summary>
/// Shows the foreground management dashboard for the packaged PrintSink app.
/// </summary>
internal sealed class ManagementScreen : Component
{
    /// <summary>
    /// Renders the management dashboard.
    /// </summary>
    /// <returns>The dashboard element tree.</returns>
    public override Element Render()
    {
        IReadOnlyList<VirtualEndpoint> endpoints = EndpointCatalog.All;
        var (selectedKind, setSelectedKind) = UseState(EndpointKind.Pdf);
        var (statusText, setStatusText) = UseState("Ready.");

        VirtualEndpoint selectedEndpoint = EndpointCatalog.GetByKind(selectedKind);
        var router = new PdlRouter();
        PdlPlan route = router.Resolve(
            PdlFormatInfo.GetContentType(selectedEndpoint.PreferredInputFormat),
            selectedEndpoint);

        return ScrollView(
            VStack(20,
                Header(),
                Overview(endpoints),
                Grid(
                    columns: [GridSize.Star(1.35), GridSize.Star()],
                    rows: [GridSize.Auto],
                    QueuesPanel(endpoints, selectedKind, setSelectedKind)
                        .Grid(row: 0, column: 0),
                    EndpointPanel(selectedEndpoint, route)
                        .Grid(row: 0, column: 1)),
                ValidationPanel(statusText, () =>
                    setStatusText($"Endpoint catalog refreshed: {endpoints.Count} queues defined."),
                    () =>
                    setStatusText("Diagnostics are local until the print activation tasks are added."))))
            .Padding(32)
            .MaxWidth(1180)
            .HAlign(HorizontalAlignment.Center);
    }

    private static StackElement Header()
    {
        return VStack(4,
            TextBlock("PrintSink")
                .ApplyStyle("TitleTextBlockStyle")
                .Bold(),
            TextBlock("A packaged virtual printer that receives jobs from Windows and routes them to file, cloud, or custom sinks.")
                .Foreground(Theme.SecondaryText)
                .Set(text => text.TextWrapping = TextWrapping.Wrap));
    }

    private static GridElement Overview(IReadOnlyList<VirtualEndpoint> endpoints)
    {
        return Grid(
            columns: [GridSize.Star(), GridSize.Star(), GridSize.Star(), GridSize.Star()],
            rows: [GridSize.Auto],
            SummaryCard("Queues", endpoints.Count.ToString(), "PDF, XPS, PS, Cloud, PWG")
                .Grid(row: 0, column: 0),
            SummaryCard("Package", "MSIX", "Windows App SDK package identity")
                .Grid(row: 0, column: 1),
            SummaryCard("Input", "OXPS / PS", "PDL routing through PrintSink.Core")
                .Grid(row: 0, column: 2),
            SummaryCard("Automation", "CLI / TUI", "System.CommandLine and Hex1b")
                .Grid(row: 0, column: 3));
    }

    private static BorderElement QueuesPanel(
        IReadOnlyList<VirtualEndpoint> endpoints,
        EndpointKind selectedKind,
        Action<EndpointKind> setSelectedKind)
    {
        return CardSurface(
            VStack(12,
                TextBlock("Queues")
                    .ApplyStyle("SubtitleTextBlockStyle")
                    .Bold(),
                VStack(8,
                    ForEach(
                        endpoints,
                        endpoint => QueueRow(endpoint, endpoint.Kind == selectedKind, setSelectedKind)
                    ))));
    }

    private static BorderElement EndpointPanel(VirtualEndpoint endpoint, PdlPlan route)
    {
        return CardSurface(
            VStack(16,
                TextBlock("Endpoint")
                    .ApplyStyle("SubtitleTextBlockStyle")
                    .Bold(),
                DetailGrid(
                    ("Queue", endpoint.QueueName),
                    ("Target", FormatPdl(endpoint.TargetFormat)),
                    ("Preferred input", FormatPdl(endpoint.PreferredInputFormat)),
                    ("Passthrough", FormatPassthrough(endpoint)),
                    ("Sink", endpoint.RequiresTargetFile ? $"Save-As file ({endpoint.DefaultExtension})" : "Application sink"),
                    ("Route", $"{route.ActionKind}: {route.Reason}")),
                Pipeline()));
    }

    private static BorderElement ValidationPanel(string statusText, Action refreshQueues, Action openDiagnostics)
    {
        return CardSurface(
            VStack(14,
                TextBlock("Validation")
                    .ApplyStyle("SubtitleTextBlockStyle")
                    .Bold(),
                TextBlock("The foreground app reads the same endpoint catalog and route planner used by the CLI and tests. OS print activation wiring lands behind the same Core contracts.")
                    .Foreground(Theme.SecondaryText)
                    .Set(text => text.TextWrapping = TextWrapping.Wrap),
                HStack(12,
                    Button("Refresh queues", refreshQueues),
                    Button("Open diagnostics", openDiagnostics)),
                TextBlock(statusText)
                    .Foreground(Theme.SecondaryText)));
    }

    private static BorderElement SummaryCard(string label, string value, string description)
    {
        return CardSurface(
            VStack(8,
                TextBlock(label)
                    .ApplyStyle("CaptionTextBlockStyle")
                    .Foreground(Theme.SecondaryText),
                TextBlock(value)
                    .ApplyStyle("TitleTextBlockStyle")
                    .Bold(),
                TextBlock(description)
                    .ApplyStyle("CaptionTextBlockStyle")
                    .Foreground(Theme.SecondaryText)
                    .Set(text => text.TextWrapping = TextWrapping.Wrap)))
            .MinHeight(104)
            .Margin(0, 0, 12, 0);
    }

    private static ButtonElement QueueRow(
        VirtualEndpoint endpoint,
        bool isSelected,
        Action<EndpointKind> setSelectedKind)
    {
        return Button(
            endpoint.QueueName,
            () => setSelectedKind(endpoint.Kind))
            .HAlign(HorizontalAlignment.Stretch)
            .AutomationName($"Select {endpoint.QueueName}")
            .Set(button =>
            {
                button.HorizontalContentAlignment = HorizontalAlignment.Left;
                button.Padding = new Thickness(12, 10, 12, 10);
                button.RequestedTheme = isSelected ? ElementTheme.Dark : ElementTheme.Default;
            })
            .ToolTip($"{FormatPdl(endpoint.PreferredInputFormat)} to {FormatPdl(endpoint.TargetFormat)}");
    }

    private static GridElement DetailGrid(params (string Label, string Value)[] rows)
    {
        return Grid(
            columns: [GridSize.Px(132), GridSize.Star()],
            rows: [.. Enumerable.Repeat(GridSize.Auto, rows.Length)],
            [.. rows
                .SelectMany((row, index) => new Element[]
                {
                    TextBlock(row.Label)
                        .Foreground(Theme.SecondaryText)
                        .Grid(row: index, column: 0),
                    TextBlock(row.Value)
                        .Set(text => text.TextWrapping = TextWrapping.Wrap)
                        .Grid(row: index, column: 1),
                })]);
    }

    private static StackElement Pipeline()
    {
        return VStack(10,
            TextBlock("Pipeline")
                .ApplyStyle("BodyStrongTextBlockStyle"),
            PipelineRow("1", "Receive PDL", "Virtual-printer workflow activation supplies source content and print ticket."),
            PipelineRow("2", "Route", "Core chooses passthrough, conversion, watermark, or rejection."),
            PipelineRow("3", "Write sink", "File and application sinks share the same job processor contract."));
    }

    private static GridElement PipelineRow(string number, string title, string description)
    {
        return Grid(
            columns: [GridSize.Px(32), GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Auto],
            TextBlock(number)
                .Bold()
                .Foreground(Theme.SecondaryText)
                .Grid(row: 0, column: 0, rowSpan: 2),
            TextBlock(title)
                .Bold()
                .Grid(row: 0, column: 1),
            TextBlock(description)
                .ApplyStyle("CaptionTextBlockStyle")
                .Foreground(Theme.SecondaryText)
                .Set(text => text.TextWrapping = TextWrapping.Wrap)
                .Grid(row: 1, column: 1));
    }

    private static BorderElement CardSurface(Element content)
    {
        return Border(content)
            .Padding(16)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke)
            .CornerRadius(8);
    }

    private static string FormatPassthrough(VirtualEndpoint endpoint)
    {
        return endpoint.PassthroughFormats.Count == 0
            ? "None"
            : string.Join(", ", endpoint.PassthroughFormats.Select(FormatPdl));
    }

    private static string FormatPdl(PdlFormat format)
    {
        return format switch
        {
            PdlFormat.Oxps => "OXPS",
            PdlFormat.Xps => "XPS",
            PdlFormat.Pdf => "PDF",
            PdlFormat.PostScript => "PostScript",
            PdlFormat.PwgRaster => "PWG Raster",
            PdlFormat.Pclm => "PCLm",
            _ => format.ToString(),
        };
    }
}
