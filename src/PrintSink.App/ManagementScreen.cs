using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using PrintSink.Core.Diagnostics;
using PrintSink.Core.Endpoints;
using PrintSink.Core.Pdl;
using PrintSink.Core.Settings;
using static Microsoft.UI.Reactor.Factories;

namespace PrintSink.App;

/// <summary>
/// Shows the foreground management dashboard for the packaged PrintSink app.
/// </summary>
internal sealed class ManagementScreen : Component
{
    private static readonly object[] EmptyDependencies = [];

    /// <summary>
    /// Renders the management dashboard.
    /// </summary>
    /// <returns>The dashboard element tree.</returns>
    public override Element Render()
    {
        IReadOnlyList<VirtualEndpoint> endpoints = EndpointCatalog.All;
        var (selectedKind, setSelectedKind) = UseState(EndpointKind.Pdf);
        var (statusText, setStatusText) = UseState("Ready.");
        var (defaultCopies, setDefaultCopies) = UseState(1.0);
        var (jobUiOptions, setJobUiOptions) = UseState(JobUiOptions.Default);
        var (diagnosticEvents, setDiagnosticEvents) = UseState(Array.Empty<DiagnosticEventRecord>());
        var (installedPrinters, setInstalledPrinters) =
            UseState<IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot>>(InstalledVirtualPrinterReader.ReadAll());

        VirtualEndpoint selectedEndpoint = EndpointCatalog.GetByKind(selectedKind);
        InstalledVirtualPrinterSnapshot selectedSnapshot = GetSnapshot(installedPrinters, selectedEndpoint);
        var router = new PdlRouter();
        PdlPlan route = router.Resolve(
            PdlFormatInfo.GetContentType(selectedEndpoint.PreferredInputFormat),
            selectedEndpoint);

        void SelectEndpoint(EndpointKind endpointKind, int? userDefaultCopies)
        {
            setSelectedKind(endpointKind);
            if (userDefaultCopies is int copies)
            {
                setDefaultCopies(copies);
            }
        }

        UseEffect(() =>
        {
            _ = LoadJobUiOptionsAsync(setJobUiOptions, setStatusText);
            _ = LoadDiagnosticsAsync(setDiagnosticEvents, setStatusText);
            return static () => { };
        }, EmptyDependencies);

        return ScrollView(
            VStack(20,
                Header(),
                Overview(endpoints, installedPrinters),
                Grid(
                    columns: [GridSize.Star(1.35), GridSize.Star()],
                    rows: [GridSize.Auto],
                    QueuesPanel(endpoints, installedPrinters, selectedKind, SelectEndpoint)
                        .Grid(row: 0, column: 0),
                    EndpointPanel(selectedEndpoint, selectedSnapshot, route)
                        .Grid(row: 0, column: 1)),
                ValidationPanel(statusText, () =>
                    _ = RefreshInstalledPrintersAsync(setInstalledPrinters, setStatusText),
                    () => _ = InstallVirtualPrintersAsync(setInstalledPrinters, setStatusText),
                    () => _ = RemoveVirtualPrintersAsync(setInstalledPrinters, setStatusText),
                    () => _ = RefreshCapabilitiesAsync(selectedKind, setInstalledPrinters, setStatusText),
                    defaultCopies,
                    setDefaultCopies,
                    selectedSnapshot.CanModifyUserDefaultPrintTicket == true,
                    () => _ = SetUserDefaultCopiesAsync(selectedKind, defaultCopies, setDefaultCopies, setInstalledPrinters, setStatusText),
                    jobUiOptions.LaunchJobUi,
                    () => _ = SaveJobUiOptionsAsync(true, setJobUiOptions, setStatusText),
                    () => _ = SaveJobUiOptionsAsync(false, setJobUiOptions, setStatusText)),
                DiagnosticsPanel(diagnosticEvents, () => _ = LoadDiagnosticsAsync(setDiagnosticEvents, setStatusText))))
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

    private static GridElement Overview(
        IReadOnlyList<VirtualEndpoint> endpoints,
        IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot> installedPrinters)
    {
        return Grid(
            columns: [GridSize.Star(), GridSize.Star(), GridSize.Star(), GridSize.Star()],
            rows: [GridSize.Auto],
            SummaryCard("Queues", $"{CountInstalled(installedPrinters)} / {endpoints.Count}", "Installed PrintSink virtual printers")
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
        IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot> installedPrinters,
        EndpointKind selectedKind,
        Action<EndpointKind, int?> selectEndpoint)
    {
        return CardSurface(
            VStack(12,
                TextBlock("Queues")
                    .ApplyStyle("SubtitleTextBlockStyle")
                    .Bold(),
                VStack(8,
                    ForEach(
                        endpoints,
                        endpoint => QueueRow(
                            endpoint,
                            GetSnapshot(installedPrinters, endpoint),
                            endpoint.Kind == selectedKind,
                            selectEndpoint)
                    ))));
    }

    private static BorderElement EndpointPanel(
        VirtualEndpoint endpoint,
        InstalledVirtualPrinterSnapshot snapshot,
        PdlPlan route)
    {
        List<(string Label, string Value)> detailRows =
        [
            ("Queue", endpoint.QueueName),
            ("Installed", snapshot.Status),
            ("Printer URI", snapshot.PrinterUri ?? endpoint.PrinterUri.ToString()),
            ("Device kind", snapshot.DeviceKind ?? "Unavailable"),
            ("Default ticket", FormatDefaultTicket(snapshot.CanModifyUserDefaultPrintTicket)),
            ("Ticket name", snapshot.UserDefaultPrintTicketName ?? "Unavailable"),
            ("Default copies", snapshot.UserDefaultCopies?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Unavailable"),
            ("Target", FormatPdl(endpoint.TargetFormat)),
            ("Preferred input", FormatPdl(endpoint.PreferredInputFormat)),
            ("Passthrough", FormatPassthrough(endpoint)),
            ("Sink", endpoint.RequiresTargetFile ? $"Save-As file ({FormatOutputExtensions(endpoint)})" : "Application sink"),
            ("Route", $"{route.ActionKind}: {route.Reason}"),
        ];

        if (!string.IsNullOrWhiteSpace(snapshot.Error))
        {
            detailRows.Add(("Stack detail", snapshot.Error));
        }

        return CardSurface(
            VStack(16,
                TextBlock("Endpoint")
                    .ApplyStyle("SubtitleTextBlockStyle")
                    .Bold(),
                DetailGrid(detailRows.ToArray()),
                Pipeline()));
    }

    private static BorderElement ValidationPanel(
        string statusText,
        Action refreshQueues,
        Action installQueues,
        Action removeQueues,
        Action refreshCapabilities,
        double defaultCopies,
        Action<double> setDefaultCopies,
        bool canSetDefaultCopies,
        Action setUserDefaultCopies,
        bool launchJobUi,
        Action enableJobUi,
        Action disableJobUi)
    {
        return CardSurface(
            VStack(14,
                TextBlock("Validation")
                    .ApplyStyle("SubtitleTextBlockStyle")
                    .Bold(),
                TextBlock("The foreground app reads the package's installed virtual printers and the same endpoint catalog used by the CLI and tests.")
                    .Foreground(Theme.SecondaryText)
                    .Set(text => text.TextWrapping = TextWrapping.Wrap),
                HStack(12,
                    Button("Install queues", installQueues)
                        .AutomationName("Install queues"),
                    Button("Remove queues", removeQueues)
                        .AutomationName("Remove queues"),
                    Button("Refresh queues", refreshQueues)
                        .AutomationName("Refresh queues"),
                    Button("Refresh capabilities", refreshCapabilities)
                        .AutomationName("Refresh capabilities")),
                HStack(12,
                    NumberBox(defaultCopies, setDefaultCopies, "Default copies")
                        .Range(1, 999)
                        .SpinButtons()
                        .AutomationName("Default copies")
                        .Width(180),
                    Button("Set default copies", setUserDefaultCopies)
                        .AutomationName("Set default copies")
                        .IsEnabled(canSetDefaultCopies),
                    Button("Enable Job UI", enableJobUi)
                        .AutomationName("Enable Job UI")
                        .IsEnabled(!launchJobUi),
                    Button("Headless jobs", disableJobUi)
                        .AutomationName("Headless jobs")
                        .IsEnabled(launchJobUi)),
                TextBlock($"Job UI: {(launchJobUi ? "Enabled" : "Headless")}")
                    .Foreground(Theme.SecondaryText),
                TextBlock(statusText)
                    .Foreground(Theme.SecondaryText)));
    }

    private static BorderElement DiagnosticsPanel(
        DiagnosticEventRecord[] diagnosticEvents,
        Action refreshDiagnostics)
    {
        Element[] eventRows = diagnosticEvents.Length == 0
            ? [TextBlock("No recent diagnostics").Foreground(Theme.SecondaryText)]
            : [.. diagnosticEvents.Select(DiagnosticRow)];

        return CardSurface(
            VStack(14,
                HStack(12,
                    TextBlock("Recent diagnostics")
                        .ApplyStyle("SubtitleTextBlockStyle")
                        .Bold(),
                    Button("Refresh", refreshDiagnostics)),
                VStack(8, eventRows)));
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
        InstalledVirtualPrinterSnapshot snapshot,
        bool isSelected,
        Action<EndpointKind, int?> selectEndpoint)
    {
        return Button(
            endpoint.QueueName,
            () => selectEndpoint(endpoint.Kind, snapshot.UserDefaultCopies))
            .HAlign(HorizontalAlignment.Stretch)
            .AutomationName($"Select {endpoint.QueueName}")
            .Set(button =>
            {
                button.HorizontalContentAlignment = HorizontalAlignment.Left;
                button.Padding = new Thickness(12, 10, 12, 10);
                button.RequestedTheme = isSelected ? ElementTheme.Dark : ElementTheme.Default;
            })
            .ToolTip($"{snapshot.Status}: {snapshot.PrinterUri ?? endpoint.PrinterUri.ToString()}");
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

    private static GridElement DiagnosticRow(DiagnosticEventRecord diagnosticEvent)
    {
        string endpoint = string.IsNullOrWhiteSpace(diagnosticEvent.Endpoint)
            ? "Package"
            : diagnosticEvent.Endpoint;
        string detail = string.IsNullOrWhiteSpace(diagnosticEvent.Detail)
            ? string.Empty
            : diagnosticEvent.Detail;

        return Grid(
            columns: [GridSize.Px(104), GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Auto],
            TextBlock(diagnosticEvent.Severity.ToString())
                .Bold()
                .Foreground(Theme.SecondaryText)
                .Grid(row: 0, column: 0, rowSpan: 2),
            TextBlock($"{endpoint}: {diagnosticEvent.Message}")
                .Bold()
                .Set(text => text.TextWrapping = TextWrapping.Wrap)
                .Grid(row: 0, column: 1),
            TextBlock(FormatDiagnosticDetail(diagnosticEvent, detail))
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

    private static string FormatOutputExtensions(VirtualEndpoint endpoint)
    {
        return endpoint.OutputExtensions.Count == 0
            ? endpoint.DefaultExtension ?? "file"
            : string.Join(", ", endpoint.OutputExtensions);
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

    private static int CountInstalled(IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot> installedPrinters)
    {
        return installedPrinters.Values.Count(snapshot => snapshot.IsInstalled);
    }

    private static InstalledVirtualPrinterSnapshot GetSnapshot(
        IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot> installedPrinters,
        VirtualEndpoint endpoint)
    {
        return installedPrinters.TryGetValue(endpoint.Kind, out InstalledVirtualPrinterSnapshot? snapshot)
            ? snapshot
            : new InstalledVirtualPrinterSnapshot(
                endpoint.Kind,
                false,
                "Unknown",
                null,
                null,
                null,
                null,
                null,
                null);
    }

    private static string FormatDefaultTicket(bool? canModifyUserDefaultPrintTicket)
    {
        return canModifyUserDefaultPrintTicket switch
        {
            true => "Mutable",
            false => "Read-only",
            null => "Unavailable",
        };
    }

    private static string FormatDiagnosticDetail(DiagnosticEventRecord diagnosticEvent, string detail)
    {
        string timestamp = diagnosticEvent.Timestamp
            .ToLocalTime()
            .ToString("u", System.Globalization.CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(detail)
            ? timestamp
            : $"{timestamp} | {detail}";
    }

    private static async Task RefreshInstalledPrintersAsync(
        Action<IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot>> setInstalledPrinters,
        Action<string> setStatusText)
    {
        try
        {
            IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot> snapshots = InstalledVirtualPrinterReader.ReadAll();
            string status = $"Installed queues refreshed: {CountInstalled(snapshots)} found.";
            await AppendDiagnosticAsync(
                    "Management UI queues refreshed",
                    null,
                    status,
                    CancellationToken.None)
                .ConfigureAwait(false);

            UiDispatch.Post(() =>
            {
                setInstalledPrinters(snapshots);
                setStatusText(status);
            });
        }
        catch (Exception ex) when (AppExceptionPolicy.IsRecoverable(ex))
        {
            UiDispatch.Post(() => setStatusText($"Queue refresh failed: {ex.Message}"));
        }
    }

    private static async Task InstallVirtualPrintersAsync(
        Action<IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot>> setInstalledPrinters,
        Action<string> setStatusText)
    {
        try
        {
            setStatusText("Installing virtual printer queues...");
            await VirtualPrinterInstaller
                .InstallAllAsync(CancellationToken.None)
                .ConfigureAwait(false);
            IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot> snapshots = InstalledVirtualPrinterReader.ReadAll();

            UiDispatch.Post(() =>
            {
                setInstalledPrinters(snapshots);
                setStatusText($"Virtual printer queues installed: {CountInstalled(snapshots)} found.");
            });
        }
        catch (Exception ex) when (AppExceptionPolicy.IsRecoverable(ex))
        {
            UiDispatch.Post(() => setStatusText($"Queue installation failed: {ex.Message}"));
        }
    }

    private static async Task RemoveVirtualPrintersAsync(
        Action<IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot>> setInstalledPrinters,
        Action<string> setStatusText)
    {
        try
        {
            setStatusText("Removing virtual printer queues...");
            await VirtualPrinterInstaller
                .RemoveAllAsync(CancellationToken.None)
                .ConfigureAwait(false);
            IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot> snapshots = InstalledVirtualPrinterReader.ReadAll();

            UiDispatch.Post(() =>
            {
                setInstalledPrinters(snapshots);
                setStatusText($"Virtual printer queues removed: {CountInstalled(snapshots)} found.");
            });
        }
        catch (Exception ex) when (AppExceptionPolicy.IsRecoverable(ex))
        {
            UiDispatch.Post(() => setStatusText($"Queue removal failed: {ex.Message}"));
        }
    }

    private static async Task RefreshCapabilitiesAsync(
        EndpointKind selectedKind,
        Action<IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot>> setInstalledPrinters,
        Action<string> setStatusText)
    {
        try
        {
            VirtualEndpoint endpoint = EndpointCatalog.GetByKind(selectedKind);
            string status = InstalledVirtualPrinterReader.RefreshCapabilities(selectedKind);
            await AppendDiagnosticAsync(
                    "Management UI capabilities refreshed",
                    endpoint.QueueName,
                    status,
                    CancellationToken.None)
                .ConfigureAwait(false);
            IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot> snapshots = InstalledVirtualPrinterReader.ReadAll();

            UiDispatch.Post(() =>
            {
                setInstalledPrinters(snapshots);
                setStatusText(status);
            });
        }
        catch (Exception ex) when (AppExceptionPolicy.IsRecoverable(ex))
        {
            UiDispatch.Post(() => setStatusText($"Capability refresh failed: {ex.Message}"));
        }
    }

    private static async Task LoadJobUiOptionsAsync(
        Action<JobUiOptions> setJobUiOptions,
        Action<string> setStatusText)
    {
        try
        {
            JobUiOptions options = await AppSettingsStoreFactory
                .Create()
                .GetJobUiOptionsAsync()
                .ConfigureAwait(false);

            UiDispatch.Post(() => setJobUiOptions(options));
        }
        catch (Exception ex) when (AppExceptionPolicy.IsRecoverable(ex))
        {
            UiDispatch.Post(() => setStatusText($"Job UI setting load failed: {ex.Message}"));
        }
    }

    private static async Task LoadDiagnosticsAsync(
        Action<DiagnosticEventRecord[]> setDiagnosticEvents,
        Action<string> setStatusText)
    {
        try
        {
            using LocalDiagnosticEventStore diagnosticEventStore = AppSettingsStoreFactory.CreateDiagnosticEventStore();
            IReadOnlyList<DiagnosticEventRecord> events = await diagnosticEventStore
                .ReadRecentAsync(8)
                .ConfigureAwait(false);

            UiDispatch.Post(() => setDiagnosticEvents([.. events]));
        }
        catch (Exception ex) when (AppExceptionPolicy.IsRecoverable(ex))
        {
            UiDispatch.Post(() => setStatusText($"Diagnostic load failed: {ex.Message}"));
        }
    }

    private static async Task SetUserDefaultCopiesAsync(
        EndpointKind selectedKind,
        double defaultCopies,
        Action<double> setDefaultCopies,
        Action<IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot>> setInstalledPrinters,
        Action<string> setStatusText)
    {
        int copies = NormalizeCopies(defaultCopies);
        try
        {
            string status = await UserDefaultPrintTicketEditor
                .SetCopiesAsync(selectedKind, copies, CancellationToken.None)
                .ConfigureAwait(false);
            await AppendDiagnosticAsync(
                    "Management UI default copies updated",
                    EndpointCatalog.GetByKind(selectedKind).QueueName,
                    status,
                    CancellationToken.None)
                .ConfigureAwait(false);
            IReadOnlyDictionary<EndpointKind, InstalledVirtualPrinterSnapshot> snapshots = InstalledVirtualPrinterReader.ReadAll();

            UiDispatch.Post(() =>
            {
                setDefaultCopies(copies);
                setInstalledPrinters(snapshots);
                setStatusText(status);
            });
        }
        catch (Exception ex) when (AppExceptionPolicy.IsRecoverable(ex))
        {
            UiDispatch.Post(() => setStatusText($"Default ticket update failed: {ex.Message}"));
        }
    }

    internal static int NormalizeCopies(double copies)
    {
        if (double.IsNaN(copies))
        {
            return 1;
        }

        return (int)Math.Clamp(Math.Round(copies, MidpointRounding.AwayFromZero), 1, 999);
    }

    private static async Task AppendDiagnosticAsync(
        string message,
        string? endpoint,
        string detail,
        CancellationToken cancellationToken)
    {
        using LocalDiagnosticEventStore diagnosticEventStore = AppSettingsStoreFactory.CreateDiagnosticEventStore();
        await diagnosticEventStore
            .AppendAsync(
                new DiagnosticEventRecord(
                    DateTimeOffset.Now,
                    DiagnosticEventSeverity.Information,
                    nameof(ManagementScreen),
                    message,
                    endpoint,
                    detail),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task SaveJobUiOptionsAsync(
        bool launchJobUi,
        Action<JobUiOptions> setJobUiOptions,
        Action<string> setStatusText)
    {
        JobUiOptions options = new(launchJobUi);
        try
        {
            await AppSettingsStoreFactory
                .Create()
                .SaveJobUiOptionsAsync(options)
                .ConfigureAwait(false);
            string status = launchJobUi ? "Job UI enabled." : "Headless jobs enabled.";
            await AppendDiagnosticAsync(
                    "Management UI Job UI mode updated",
                    null,
                    status,
                    CancellationToken.None)
                .ConfigureAwait(false);

            UiDispatch.Post(() =>
            {
                setJobUiOptions(options);
                setStatusText(status);
            });
        }
        catch (Exception ex) when (AppExceptionPolicy.IsRecoverable(ex))
        {
            UiDispatch.Post(() => setStatusText($"Job UI setting save failed: {ex.Message}"));
        }
    }
}
