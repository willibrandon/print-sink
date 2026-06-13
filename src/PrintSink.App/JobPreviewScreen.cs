using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using PrintSink.Core.Diagnostics;
using PrintSink.Core.Settings;
using PrintSink.Core.Watermark;
using Windows.Graphics.Printing.Workflow;
using Windows.Storage;
using static Microsoft.UI.Reactor.Factories;

namespace PrintSink.App;

/// <summary>
/// Shows the Print Support job UI activation surface.
/// </summary>
internal sealed class JobPreviewScreen : Component<AppActivationRoute>
{
    private static readonly object[] EmptyDependencies = [];

    /// <summary>
    /// Renders the job preview screen.
    /// </summary>
    /// <returns>The job preview element tree.</returns>
    public override Element Render()
    {
        var (status, setStatus) = UseState("Waiting for print workflow data.");
        var (jobTitle, setJobTitle) = UseState("Pending job");
        var (source, setSource) = UseState("Unknown source");
        var (contentType, setContentType) = UseState("No PDL received yet.");
        var (canContinue, setCanContinue) = UseState(false);
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
        var (jobPassword, setJobPassword) = UseState(string.Empty);
        var (passwordEncryptionIndex, setPasswordEncryptionIndex) = UseState(0);
        Ref<JobUiDeferralState> jobState = UseRef(new JobUiDeferralState());
        ReactorWindow? window = UseWindow();
        string[] passwordEncryptionMethods = ["sha2-256", "none"];

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
            PrintWorkflowJobActivatedEventArgs? jobArgs = Props.JobArgs;
            if (jobArgs is null)
            {
                setStatus("No job activation payload was supplied.");
                return static () => { };
            }

            PrintWorkflowJobUISession session = jobArgs.Session;

            void OnVirtualPrinterDataAvailable(
                PrintWorkflowJobUISession sender,
                PrintWorkflowVirtualPrinterUIEventArgs args)
            {
                var deferral = args.GetDeferral();
                try
                {
                    jobState.Current.SetPdl(args.Configuration, deferral);
                    UiDispatch.Post(() =>
                    {
                        setStatus("Virtual printer PDL received.");
                        setJobTitle(args.Configuration.JobTitle);
                        setSource(args.Configuration.SourceAppDisplayName);
                        setContentType(args.SourceContent.ContentType);
                        setCanContinue(true);
                    });
                }
                catch
                {
                    jobState.Current.CompleteAll();
                    throw;
                }
            }

            void OnPdlDataAvailable(
                PrintWorkflowJobUISession sender,
                PrintWorkflowPdlDataAvailableEventArgs args)
            {
                var deferral = args.GetDeferral();
                try
                {
                    jobState.Current.SetPdl(args.Configuration, deferral);
                    UiDispatch.Post(() =>
                    {
                        setStatus("Printer workflow PDL received.");
                        setJobTitle(args.Configuration.JobTitle);
                        setSource(args.Configuration.SourceAppDisplayName);
                        setContentType(args.SourceContent.ContentType);
                        setCanContinue(true);
                    });
                }
                catch
                {
                    jobState.Current.CompleteAll();
                    throw;
                }
            }

            void OnJobNotification(
                PrintWorkflowJobUISession sender,
                PrintWorkflowJobNotificationEventArgs args)
            {
                var deferral = args.GetDeferral();
                try
                {
                    jobState.Current.SetNotification(deferral);
                    UiDispatch.Post(() => setStatus("Job notification received."));
                }
                catch
                {
                    jobState.Current.CompleteAll();
                    throw;
                }
            }

            session.VirtualPrinterUIDataAvailable += OnVirtualPrinterDataAvailable;
            session.PdlDataAvailable += OnPdlDataAvailable;
            session.JobNotification += OnJobNotification;
            session.Start();

            return () =>
            {
                session.VirtualPrinterUIDataAvailable -= OnVirtualPrinterDataAvailable;
                session.PdlDataAvailable -= OnPdlDataAvailable;
                session.JobNotification -= OnJobNotification;
                jobState.Current.CompleteAll();
            };
        }, EmptyDependencies);

        return ScrollView(
            VStack(20,
                TitleBlock("Job preview", "Activated through the Print Support Job UI contract."),
                CardSurface(
                    VStack(12,
                        TextBlock("Current job")
                            .ApplyStyle("SubtitleTextBlockStyle")
                            .Bold(),
                        DetailGrid(
                            ("Status", status),
                            ("Job", jobTitle),
                            ("Source", source),
                            ("Content type", contentType)))),
                CardSurface(
                    VStack(12,
                        TextBlock("Preview")
                            .ApplyStyle("SubtitleTextBlockStyle")
                            .Bold(),
                        TextBlock("Configure the watermark for this job before the background task resumes the stream.")
                            .Foreground(Theme.SecondaryText)
                            .Set(text => text.TextWrapping = TextWrapping.Wrap),
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
                        PasswordBox(jobPassword, setJobPassword, "Job password")
                            .AutomationName("Job password"),
                        ComboBox(passwordEncryptionMethods, passwordEncryptionIndex, setPasswordEncryptionIndex)
                            .AutomationName("Job password encryption"),
                        HStack(12,
                            Button(
                                "Continue",
                                () => _ = ContinueJobAsync(
                                    jobState.Current,
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
                                    jobPassword,
                                    passwordEncryptionMethods[passwordEncryptionIndex],
                                    setStatus))
                                .IsEnabled(canContinue),
                            Button(
                                "Cancel",
                                () => _ = CancelJobAsync(jobState.Current, jobTitle, source, setStatus))
                                .IsEnabled(canContinue)))))
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
            columns: [GridSize.Px(132), GridSize.Star()],
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

    private static async Task ContinueJobAsync(
        JobUiDeferralState jobState,
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
        string jobPassword,
        string passwordEncryptionMethod,
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
            WatermarkOptions watermarkOptions = textWatermark is null && imageWatermark is null
                ? WatermarkOptions.Disabled
                : new WatermarkOptions(true, textWatermark, imageWatermark);
            JobPasswordOptions? jobPasswordOptions = string.IsNullOrWhiteSpace(jobPassword)
                ? null
                : JobPasswordOptions.FromPassword(jobPassword, passwordEncryptionMethod);
            JobProcessingOptions options = new(watermarkOptions, jobPasswordOptions);
            await AppSettingsStoreFactory
                .Create()
                .SaveJobProcessingOptionsAsync(options)
                .ConfigureAwait(false);

            UiDispatch.Post(() =>
            {
                setStatus("Continuing print job.");
                jobState.CompleteAll();
                Microsoft.UI.Xaml.Application.Current.Exit();
            });
        }
        catch (Exception ex)
        {
            UiDispatch.Post(() => setStatus($"Continue failed: {ex.Message}"));
        }
    }

    private static async Task CancelJobAsync(
        JobUiDeferralState jobState,
        string jobTitle,
        string source,
        Action<string> setStatus)
    {
        try
        {
            await AppSettingsStoreFactory
                .CreateDiagnosticEventStore()
                .AppendAsync(
                    new DiagnosticEventRecord(
                        DateTimeOffset.UtcNow,
                        DiagnosticEventSeverity.Warning,
                        nameof(JobPreviewScreen),
                        "Job canceled",
                        null,
                        $"User canceled from Job UI. Job: {jobTitle}; Source: {source}."))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            UiDispatch.Post(() => setStatus($"Cancel diagnostic failed: {ex.Message}"));
        }
        finally
        {
            UiDispatch.Post(() =>
            {
                jobState.AbortAndComplete();
                Microsoft.UI.Xaml.Application.Current.Exit();
            });
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
