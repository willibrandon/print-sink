using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Watermark;
using Windows.Graphics.Printing.PrintSupport;
using static Microsoft.UI.Reactor.Factories;

namespace PrintSink.App;

/// <summary>
/// Shows the Print Support settings activation surface.
/// </summary>
internal sealed class SettingsScreen : Component<AppActivationRoute>
{
    private static readonly object[] EmptyDependencies = [];

    /// <summary>
    /// Renders the settings activation screen.
    /// </summary>
    /// <returns>The settings screen element tree.</returns>
    public override Element Render()
    {
        IReadOnlyList<VirtualEndpoint> endpoints = EndpointCatalog.All;
        PrintSupportSettingsActivatedEventArgs? settingsArgs = Props.SettingsArgs;
        string launchKind = settingsArgs?.Session.LaunchKind.ToString() ?? "Unavailable";
        string ownerWindowId = settingsArgs?.OwnerWindowId.Value.ToString() ?? "Unavailable";
        var (selectedIndex, setSelectedIndex) = UseState(0);
        var (enabled, setEnabled) = UseState(false);
        var (text, setText) = UseState("Confidential");
        var (fontSize, setFontSize) = UseState(48d);
        var (opacity, setOpacity) = UseState(0.28d);
        var (rotation, setRotation) = UseState(-30d);
        var (status, setStatus) = UseState("Ready.");
        VirtualEndpoint selectedEndpoint = endpoints[selectedIndex];

        UseEffect(() =>
        {
            _ = LoadWatermarkAsync(selectedEndpoint, setEnabled, setText, setFontSize, setOpacity, setRotation, setStatus);
            return static () => { };
        }, EmptyDependencies);

        return ScrollView(
            VStack(20,
                TitleBlock("Print preferences", "Activated through the Print Support Settings UI contract."),
                CardSurface(
                    VStack(12,
                        TextBlock("Activation")
                            .ApplyStyle("SubtitleTextBlockStyle")
                            .Bold(),
                        DetailGrid(
                            ("Launch kind", launchKind),
                            ("Owner window", ownerWindowId),
                            ("Session", settingsArgs is null ? "Not available" : "Connected")))),
                CardSurface(
                    VStack(14,
                        TextBlock("Watermark")
                            .ApplyStyle("SubtitleTextBlockStyle")
                            .Bold(),
                        ComboBox(
                            [.. endpoints.Select(endpoint => endpoint.QueueName)],
                            selectedIndex,
                            index =>
                            {
                                if (index < 0 || index >= endpoints.Count)
                                {
                                    return;
                                }

                                setSelectedIndex(index);
                                _ = LoadWatermarkAsync(endpoints[index], setEnabled, setText, setFontSize, setOpacity, setRotation, setStatus);
                            })
                            .AutomationName("Endpoint"),
                        ToggleSwitch(enabled, setEnabled, "On", "Off", "Text watermark"),
                        TextBox(text, setText, "Text", "Watermark text")
                            .AutomationName("Watermark text"),
                        Grid(
                            columns: [GridSize.Star(), GridSize.Star(), GridSize.Star()],
                            rows: [GridSize.Auto],
                            NumberBox(fontSize, value => setFontSize(Clamp(value, 8, 200)), "Font size")
                                .AutomationName("Font size")
                                .Grid(row: 0, column: 0),
                            NumberBox(opacity, value => setOpacity(Clamp(value, 0.05, 1)), "Opacity")
                                .AutomationName("Opacity")
                                .Grid(row: 0, column: 1),
                            NumberBox(rotation, value => setRotation(Clamp(value, -180, 180)), "Rotation")
                                .AutomationName("Rotation")
                                .Grid(row: 0, column: 2)),
                        HStack(12,
                            Button(
                                "Save",
                                () => _ = SaveWatermarkAsync(
                                    selectedEndpoint,
                                    enabled,
                                    text,
                                    fontSize,
                                    opacity,
                                    rotation,
                                    setStatus)),
                            Button("Close", Microsoft.UI.Xaml.Application.Current.Exit)),
                        TextBlock(status)
                            .Foreground(Theme.SecondaryText)
                            .Set(block => block.TextWrapping = TextWrapping.Wrap))))
            .Padding(32)
            .MaxWidth(920)
            .HAlign(HorizontalAlignment.Center));
    }

    private static StackElement TitleBlock(string title, string subtitle)
    {
        return VStack(4,
            TextBlock(title)
                .ApplyStyle("TitleTextBlockStyle")
                .Bold(),
            TextBlock(subtitle)
                .Foreground(Theme.SecondaryText)
                .Set(text => text.TextWrapping = TextWrapping.Wrap));
    }

    private static GridElement DetailGrid(params (string Label, string Value)[] rows)
    {
        return Grid(
            columns: [GridSize.Px(140), GridSize.Star()],
            rows: [.. Enumerable.Repeat(GridSize.Auto, rows.Length)],
            [.. rows.SelectMany((row, index) => new Element[]
            {
                TextBlock(row.Label)
                    .Foreground(Theme.SecondaryText)
                    .Grid(row: index, column: 0),
                TextBlock(row.Value)
                    .Set(text => text.TextWrapping = TextWrapping.Wrap)
                    .Grid(row: index, column: 1),
            })]);
    }

    private static BorderElement CardSurface(Element content)
    {
        return Border(content)
            .Padding(16)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke)
            .CornerRadius(8);
    }

    private static async Task LoadWatermarkAsync(
        VirtualEndpoint endpoint,
        Action<bool> setEnabled,
        Action<string> setText,
        Action<double> setFontSize,
        Action<double> setOpacity,
        Action<double> setRotation,
        Action<string> setStatus)
    {
        try
        {
            WatermarkOptions options = await AppSettingsStoreFactory
                .Create()
                .GetWatermarkOptionsAsync(endpoint.PrinterUri)
                .ConfigureAwait(false);

            UiDispatch.Post(() =>
            {
                setEnabled(options.Enabled);
                setText(options.Text?.Text ?? "Confidential");
                setFontSize(options.Text?.FontSize ?? 48);
                setOpacity(options.Text?.Opacity ?? 0.28);
                setRotation(options.Text?.RotationDegrees ?? -30);
                setStatus($"Loaded settings for {endpoint.QueueName}.");
            });
        }
        catch (Exception ex)
        {
            UiDispatch.Post(() => setStatus($"Load failed: {ex.Message}"));
        }
    }

    private static async Task SaveWatermarkAsync(
        VirtualEndpoint endpoint,
        bool enabled,
        string text,
        double fontSize,
        double opacity,
        double rotation,
        Action<string> setStatus)
    {
        if (enabled && string.IsNullOrWhiteSpace(text))
        {
            setStatus("Watermark text is required when watermarking is on.");
            return;
        }

        WatermarkOptions options = enabled
            ? new WatermarkOptions(
                true,
                new TextWatermark(
                    text.Trim(),
                    "Segoe UI",
                    Clamp(fontSize, 8, 200),
                    Clamp(opacity, 0.05, 1),
                    Clamp(rotation, -180, 180),
                    0,
                    0),
                null)
            : WatermarkOptions.Disabled;

        try
        {
            await AppSettingsStoreFactory
                .Create()
                .SaveWatermarkOptionsAsync(endpoint.PrinterUri, options)
                .ConfigureAwait(false);

            UiDispatch.Post(() => setStatus($"Saved settings for {endpoint.QueueName}."));
        }
        catch (Exception ex)
        {
            UiDispatch.Post(() => setStatus($"Save failed: {ex.Message}"));
        }
    }

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value))
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }
}
