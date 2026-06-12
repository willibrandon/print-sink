using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using PrintSink.Core.Settings;
using PrintSink.Core.Watermark;
using Windows.Foundation;
using Windows.Graphics.Printing.Workflow;
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
        var (enabled, setEnabled) = UseState(false);
        var (text, setText) = UseState("Confidential");
        var (fontSize, setFontSize) = UseState(48d);
        var (opacity, setOpacity) = UseState(0.28d);
        var (rotation, setRotation) = UseState(-30d);
        Ref<JobUiDeferralState> jobState = UseRef(new JobUiDeferralState());

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
                        ToggleSwitch(enabled, setEnabled, "On", "Off", "Text watermark"),
                        TextBox(text, setText, "Text", "Watermark text")
                            .AutomationName("Watermark text")
                            .IsEnabled(enabled),
                        Grid(
                            columns: [GridSize.Star(), GridSize.Star(), GridSize.Star()],
                            rows: [GridSize.Auto],
                            NumberBox(fontSize, value => setFontSize(Clamp(value, 8, 200)), "Font size")
                                .AutomationName("Font size")
                                .IsEnabled(enabled)
                                .Grid(row: 0, column: 0),
                            NumberBox(opacity, value => setOpacity(Clamp(value, 0.05, 1)), "Opacity")
                                .AutomationName("Opacity")
                                .IsEnabled(enabled)
                                .Grid(row: 0, column: 1),
                            NumberBox(rotation, value => setRotation(Clamp(value, -180, 180)), "Rotation")
                                .AutomationName("Rotation")
                                .IsEnabled(enabled)
                                .Grid(row: 0, column: 2)),
                        HStack(12,
                            Button(
                                "Continue",
                                () => _ = ContinueJobAsync(
                                    jobState.Current,
                                    enabled,
                                    text,
                                    fontSize,
                                    opacity,
                                    rotation,
                                    setStatus))
                                .IsEnabled(canContinue),
                            Button(
                                "Cancel",
                                () => CancelJob(jobState.Current))
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

        WatermarkOptions watermarkOptions = enabled
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
            JobProcessingOptions options = new(watermarkOptions);
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

    private static void CancelJob(JobUiDeferralState jobState)
    {
        jobState.AbortAndComplete();
        Microsoft.UI.Xaml.Application.Current.Exit();
    }

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value))
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }

    private sealed class JobUiDeferralState
    {
        private PrintWorkflowConfiguration? configuration;
        private Deferral? pdlDeferral;
        private Deferral? notificationDeferral;

        public void SetPdl(PrintWorkflowConfiguration nextConfiguration, Deferral nextDeferral)
        {
            ArgumentNullException.ThrowIfNull(nextConfiguration);
            ArgumentNullException.ThrowIfNull(nextDeferral);

            configuration = nextConfiguration;
            CompletePdl();
            pdlDeferral = nextDeferral;
        }

        public void SetNotification(Deferral nextDeferral)
        {
            ArgumentNullException.ThrowIfNull(nextDeferral);

            CompleteNotification();
            notificationDeferral = nextDeferral;
        }

        public void AbortAndComplete()
        {
            configuration?.AbortPrintFlow(PrintWorkflowJobAbortReason.UserCanceled);
            CompleteAll();
        }

        public void CompleteAll()
        {
            CompletePdl();
            CompleteNotification();
        }

        private void CompletePdl()
        {
            pdlDeferral?.Complete();
            pdlDeferral = null;
        }

        private void CompleteNotification()
        {
            notificationDeferral?.Complete();
            notificationDeferral = null;
        }
    }
}
