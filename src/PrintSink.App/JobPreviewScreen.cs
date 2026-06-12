using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
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
                    UiDispatch.Post(() =>
                    {
                        setStatus("Virtual printer PDL received.");
                        setJobTitle(args.Configuration.JobTitle);
                        setSource(args.Configuration.SourceAppDisplayName);
                        setContentType(args.SourceContent.ContentType);
                    });
                }
                finally
                {
                    deferral.Complete();
                }
            }

            void OnPdlDataAvailable(
                PrintWorkflowJobUISession sender,
                PrintWorkflowPdlDataAvailableEventArgs args)
            {
                var deferral = args.GetDeferral();
                try
                {
                    UiDispatch.Post(() =>
                    {
                        setStatus("Printer workflow PDL received.");
                        setJobTitle(args.Configuration.JobTitle);
                        setSource(args.Configuration.SourceAppDisplayName);
                        setContentType(args.SourceContent.ContentType);
                    });
                }
                finally
                {
                    deferral.Complete();
                }
            }

            void OnJobNotification(
                PrintWorkflowJobUISession sender,
                PrintWorkflowJobNotificationEventArgs args)
            {
                var deferral = args.GetDeferral();
                try
                {
                    UiDispatch.Post(() => setStatus("Job notification received."));
                }
                finally
                {
                    deferral.Complete();
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
                        TextBlock("The UI session captures print workflow metadata before the background task resumes the job.")
                            .Foreground(Theme.SecondaryText)
                            .Set(text => text.TextWrapping = TextWrapping.Wrap),
                        Button("Close", Microsoft.UI.Xaml.Application.Current.Exit))))
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
}
