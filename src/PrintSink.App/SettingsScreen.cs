using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Watermark;
using Windows.Graphics.Printing.PrintSupport;
using Windows.Storage;
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
        var (textEnabled, setTextEnabled) = UseState(false);
        var (text, setText) = UseState("Confidential");
        var (fontSize, setFontSize) = UseState(48d);
        var (opacity, setOpacity) = UseState(0.28d);
        var (rotation, setRotation) = UseState(-30d);
        var (imageEnabled, setImageEnabled) = UseState(false);
        var (imagePath, setImagePath) = UseState(string.Empty);
        var (imageWidth, setImageWidth) = UseState(144d);
        var (imageHeight, setImageHeight) = UseState(144d);
        var (imageOpacity, setImageOpacity) = UseState(0.28d);
        var (imageRotation, setImageRotation) = UseState(0d);
        var (status, setStatus) = UseState("Ready.");
        var (windowMode, setWindowMode) = UseState("Pending.");
        ReactorWindow? window = UseWindow();
        VirtualEndpoint selectedEndpoint = endpoints[selectedIndex];

        async Task PickImageAsync()
        {
            if (window is null)
            {
                setStatus("Image picker is unavailable.");
                return;
            }

            StorageFile? file = await WatermarkImagePicker
                .PickAsync(window.NativeWindow)
                .ConfigureAwait(true);
            if (file is not null)
            {
                setImagePath(file.Path);
                setImageEnabled(true);
            }
        }

        UseEffect(() =>
        {
            setWindowMode(SettingsWindowOwner.Apply(window, settingsArgs));
            _ = LoadWatermarkAsync(
                selectedEndpoint,
                setTextEnabled,
                setText,
                setFontSize,
                setOpacity,
                setRotation,
                setImageEnabled,
                setImagePath,
                setImageWidth,
                setImageHeight,
                setImageOpacity,
                setImageRotation,
                setStatus);
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
                            ("Window mode", windowMode),
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
                                _ = LoadWatermarkAsync(
                                    endpoints[index],
                                    setTextEnabled,
                                    setText,
                                    setFontSize,
                                    setOpacity,
                                    setRotation,
                                    setImageEnabled,
                                    setImagePath,
                                    setImageWidth,
                                    setImageHeight,
                                    setImageOpacity,
                                    setImageRotation,
                                    setStatus);
                            })
                            .AutomationName("Endpoint"),
                        ToggleSwitch(textEnabled, setTextEnabled, "On", "Off", "Text watermark"),
                        TextBox(text, setText, "Text", "Watermark text")
                            .AutomationName("Watermark text")
                            .IsEnabled(textEnabled),
                        Grid(
                            columns: [GridSize.Star(), GridSize.Star(), GridSize.Star()],
                            rows: [GridSize.Auto],
                            NumberBox(fontSize, value => setFontSize(Clamp(value, 8, 200)), "Font size")
                                .AutomationName("Font size")
                                .IsEnabled(textEnabled)
                                .Grid(row: 0, column: 0),
                            NumberBox(opacity, value => setOpacity(Clamp(value, 0.05, 1)), "Opacity")
                                .AutomationName("Opacity")
                                .IsEnabled(textEnabled)
                                .Grid(row: 0, column: 1),
                            NumberBox(rotation, value => setRotation(Clamp(value, -180, 180)), "Rotation")
                                .AutomationName("Rotation")
                                .IsEnabled(textEnabled)
                                .Grid(row: 0, column: 2)),
                        ToggleSwitch(imageEnabled, setImageEnabled, "On", "Off", "Image watermark"),
                        Grid(
                            columns: [GridSize.Star(), GridSize.Auto],
                            rows: [GridSize.Auto],
                            TextBox(imagePath, setImagePath, "Path", "Image path")
                                .AutomationName("Image path")
                                .IsEnabled(imageEnabled)
                                .Grid(row: 0, column: 0),
                            Button("Browse", () => _ = PickImageAsync())
                                .IsEnabled(imageEnabled)
                                .Grid(row: 0, column: 1)),
                        Grid(
                            columns: [GridSize.Star(), GridSize.Star(), GridSize.Star(), GridSize.Star()],
                            rows: [GridSize.Auto],
                            NumberBox(imageWidth, value => setImageWidth(Clamp(value, 1, 4096)), "Width")
                                .AutomationName("Image width")
                                .IsEnabled(imageEnabled)
                                .Grid(row: 0, column: 0),
                            NumberBox(imageHeight, value => setImageHeight(Clamp(value, 1, 4096)), "Height")
                                .AutomationName("Image height")
                                .IsEnabled(imageEnabled)
                                .Grid(row: 0, column: 1),
                            NumberBox(imageOpacity, value => setImageOpacity(Clamp(value, 0.05, 1)), "Opacity")
                                .AutomationName("Image opacity")
                                .IsEnabled(imageEnabled)
                                .Grid(row: 0, column: 2),
                            NumberBox(imageRotation, value => setImageRotation(Clamp(value, -180, 180)), "Rotation")
                                .AutomationName("Image rotation")
                                .IsEnabled(imageEnabled)
                                .Grid(row: 0, column: 3)),
                        HStack(12,
                            Button(
                                "Save",
                                () => _ = SaveWatermarkAsync(
                                    selectedEndpoint,
                                    textEnabled,
                                    text,
                                    fontSize,
                                    opacity,
                                    rotation,
                                    imageEnabled,
                                    imagePath,
                                    imageWidth,
                                    imageHeight,
                                    imageOpacity,
                                    imageRotation,
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
        Action<bool> setTextEnabled,
        Action<string> setText,
        Action<double> setFontSize,
        Action<double> setOpacity,
        Action<double> setRotation,
        Action<bool> setImageEnabled,
        Action<string> setImagePath,
        Action<double> setImageWidth,
        Action<double> setImageHeight,
        Action<double> setImageOpacity,
        Action<double> setImageRotation,
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
                setTextEnabled(options.Text is not null);
                setText(options.Text?.Text ?? "Confidential");
                setFontSize(options.Text?.FontSize ?? 48);
                setOpacity(options.Text?.Opacity ?? 0.28);
                setRotation(options.Text?.RotationDegrees ?? -30);
                setImageEnabled(options.Image is not null);
                setImagePath(options.Image?.ImagePath ?? string.Empty);
                setImageWidth(options.Image?.Width ?? 144);
                setImageHeight(options.Image?.Height ?? 144);
                setImageOpacity(options.Image?.Opacity ?? 0.28);
                setImageRotation(options.Image?.RotationDegrees ?? 0);
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
        bool textEnabled,
        string text,
        double fontSize,
        double opacity,
        double rotation,
        bool imageEnabled,
        string imagePath,
        double imageWidth,
        double imageHeight,
        double imageOpacity,
        double imageRotation,
        Action<string> setStatus)
    {
        if (textEnabled && string.IsNullOrWhiteSpace(text))
        {
            setStatus("Watermark text is required when watermarking is on.");
            return;
        }

        if (imageEnabled && string.IsNullOrWhiteSpace(imagePath))
        {
            setStatus("Image path is required when image watermarking is on.");
            return;
        }

        try
        {
            TextWatermark? textWatermark = textEnabled
                ? new TextWatermark(
                    text.Trim(),
                    "Segoe UI",
                    Clamp(fontSize, 8, 200),
                    Clamp(opacity, 0.05, 1),
                    Clamp(rotation, -180, 180),
                    0,
                    0)
                : null;
            ImageWatermark? imageWatermark = imageEnabled
                ? await WatermarkImageStorage
                    .CreateImageWatermarkAsync(
                        imagePath.Trim(),
                        Clamp(imageWidth, 1, 4096),
                        Clamp(imageHeight, 1, 4096),
                        Clamp(imageOpacity, 0.05, 1),
                        Clamp(imageRotation, -180, 180),
                        0,
                        0)
                    .ConfigureAwait(false)
                : null;
            WatermarkOptions options = textWatermark is null && imageWatermark is null
                ? WatermarkOptions.Disabled
                : new WatermarkOptions(true, textWatermark, imageWatermark);

            await AppSettingsStoreFactory
                .Create()
                .SaveWatermarkOptionsAsync(endpoint.PrinterUri, options)
                .ConfigureAwait(false);

            string refreshStatus;
            try
            {
                refreshStatus = InstalledVirtualPrinterReader.RefreshCapabilities(endpoint.Kind);
            }
            catch (Exception ex)
            {
                refreshStatus = $"Capability refresh failed: {ex.Message}";
            }

            UiDispatch.Post(() => setStatus($"Saved settings for {endpoint.QueueName}. {refreshStatus}"));
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
