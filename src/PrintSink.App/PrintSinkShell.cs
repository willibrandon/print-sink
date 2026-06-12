namespace PrintSink;

using System.Globalization;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using PrintSink.Endpoints;
using PrintSink.Pdl;
using static Microsoft.UI.Reactor.Core.Theme;
using static Microsoft.UI.Reactor.Factories;

internal sealed class PrintSinkShell : Component
{
    public override Element Render()
    {
        IReadOnlyList<VirtualEndpoint> endpoints = EndpointCatalog.BuiltInQueues;
        int readyCount = endpoints.Count(endpoint => endpoint.Kind is EndpointKind.Pdf or EndpointKind.Xps or EndpointKind.PostScript);

        return FlexColumn(
                (TitleBar("PrintSink") with { Subtitle = "Virtual printer management" }).Flex(shrink: 0),
                ScrollView(
                    Grid(
                            columns: [GridSize.Star(), GridSize.Auto, GridSize.Star()],
                            rows: [GridSize.Auto],
                            FlexColumn(
                                    Header(),
                                    SummaryGrid(endpoints.Count, readyCount),
                                    MainGrid(endpoints),
                                    ValidationPanel())
                                .MaxWidth(1040)
                                .Margin(24, 20, 24, 32)
                                .Grid(row: 0, column: 1))
                        .Flex(grow: 1)))
            .Backdrop(BackdropKind.Mica);
    }

    private static Element Header() =>
        FlexColumn(
                TextBlock("PrintSink")
                    .FontSize(28)
                    .Set(text => text.FontWeight = FontWeights.SemiBold),
                TextBlock("Packaged WinUI shell for virtual printer routing.")
                    .Foreground(SecondaryText)
                    .Set(text => text.TextWrapping = TextWrapping.Wrap))
            .Margin(0, 0, 0, 16);

    private static Element SummaryGrid(int queueCount, int readyCount) =>
        (Grid(
            columns: [GridSize.Star(), GridSize.Star(), GridSize.Star(), GridSize.Star()],
            rows: [GridSize.Auto],
            StatCard("Queues", queueCount.ToString(CultureInfo.InvariantCulture), "PDF, XPS, PS, Cloud, PWG")
                .Grid(row: 0, column: 0),
            StatCard("Ready", readyCount.ToString(CultureInfo.InvariantCulture), "Core routes")
                .Grid(row: 0, column: 1),
            StatCard("Package", "MSIX", "WinUI 3 app")
                .Grid(row: 0, column: 2),
            StatCard("UI", "Reactor", "Code-first shell")
                .Grid(row: 0, column: 3)) with
        { ColumnSpacing = 12 })
        .Margin(0, 0, 0, 12);

    private static Element MainGrid(IReadOnlyList<VirtualEndpoint> endpoints) =>
        (Grid(
            columns: [GridSize.Star(), GridSize.Star()],
            rows: [GridSize.Auto],
            Panel("Queues", FlexColumn(endpoints.Select(QueueRow).ToArray<Element?>()))
                .Grid(row: 0, column: 0),
            Panel(
                    "Pipeline",
                    FlexColumn(
                        PipelineStep("1", "Receive PDL", "Source stream and content type enter the core router."),
                        PipelineStep("2", "Plan", "Core chooses copy, conversion, or rejection."),
                        PipelineStep("3", "Write sink", "File and custom sinks share one async contract.")))
                .Grid(row: 0, column: 1)) with
        { ColumnSpacing = 12 })
        .Margin(0, 0, 0, 12);

    private static Element ValidationPanel() =>
        Panel(
            "Validation",
            FlexColumn(
                    TextBlock("The baseline keeps print activation out of process until the core contracts are stable.")
                        .Foreground(SecondaryText)
                        .Set(text => text.TextWrapping = TextWrapping.Wrap),
                    (FlexRow(
                        Button("Refresh queues"),
                        Button("Open diagnostics")) with
                    { ColumnGap = 8 }))
                with
            { RowGap = 12 });

    private static Element StatCard(string label, string value, string caption) =>
        Border(
                (FlexColumn(
                    Caption(label).Foreground(SecondaryText),
                    TextBlock(value)
                        .FontSize(26)
                        .Set(text => text.FontWeight = FontWeights.SemiBold),
                    Caption(caption).Foreground(SecondaryText)) with
                { RowGap = 6 }))
            .Padding(16)
            .Background(CardBackground)
            .WithBorder(CardStroke, 1)
            .CornerRadius(8);

    private static Element Panel(string title, Element content) =>
        Border(
                (FlexColumn(
                    TextBlock(title)
                        .FontSize(20)
                        .Set(text => text.FontWeight = FontWeights.SemiBold),
                    content) with
                { RowGap = 12 }))
            .Padding(18)
            .Background(CardBackground)
            .WithBorder(CardStroke, 1)
            .CornerRadius(8);

    private static Element QueueRow(VirtualEndpoint endpoint) =>
        Border(
                (FlexRow(
                    (FlexColumn(
                        TextBlock(endpoint.DisplayName)
                            .Set(text => text.FontWeight = FontWeights.SemiBold),
                        Caption($"{endpoint.TargetFormat}: {endpoint.Description}")
                            .Foreground(SecondaryText)) with
                    { RowGap = 4 })
                    .Flex(grow: 1),
                    StatusPill(endpoint.Kind)) with
                { AlignItems = FlexAlign.Center, ColumnGap = 12 }))
            .Padding(12)
            .Background(ControlFill)
            .CornerRadius(6)
            .Margin(0, 0, 0, 8);

    private static Element StatusPill(EndpointKind kind)
    {
        bool ready = kind is EndpointKind.Pdf or EndpointKind.Xps or EndpointKind.PostScript;

        return Border(Caption(ready ? "Ready" : "Planned")
                .Foreground(ready ? SystemSuccess : SystemCaution))
            .Padding(10, 4, 10, 4)
            .Background(ready ? SystemSuccessBackground : SystemCautionBackground)
            .CornerRadius(12)
            .Flex(shrink: 0);
    }

    private static Element PipelineStep(string number, string title, string body) =>
        (FlexRow(
            Border(TextBlock(number).HAlign(HorizontalAlignment.Center))
                .Width(32)
                .Height(32)
                .Background(ControlFillSecondary)
                .CornerRadius(16)
                .Flex(shrink: 0),
            (FlexColumn(
                TextBlock(title).Set(text => text.FontWeight = FontWeights.SemiBold),
                Caption(body)
                    .Foreground(SecondaryText)
                    .Set(text => text.TextWrapping = TextWrapping.Wrap)) with
            { RowGap = 4 })) with
        { ColumnGap = 12 })
        .Margin(0, 0, 0, 12);
}
