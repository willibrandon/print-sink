using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Printing;
using Windows.Foundation;
using Windows.Graphics.Printing;
using WinRT.Interop;
using XamlControls = Microsoft.UI.Xaml.Controls;

namespace PrintSink.App;

internal sealed class WinRtPrintSourceSession
{
    private const double PageWidth = 816;
    private const double PageHeight = 1056;

    private readonly string sourceText;
    private readonly PrintDocument printDocument = new();
    private readonly IPrintDocumentSource documentSource;
    private XamlControls.Border? page;
    private PrintManager? printManager;

    internal WinRtPrintSourceSession(string sourceText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceText);

        this.sourceText = sourceText;
        documentSource = printDocument.DocumentSource;
    }

    internal async Task ShowAsync(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!PrintManager.IsSupported())
        {
            throw new InvalidOperationException("Windows printing is not available for this session.");
        }

        nint windowHandle = WindowNative.GetWindowHandle(window);
        printManager = PrintManagerInterop.GetForWindow(windowHandle);
        printManager.PrintTaskRequested += OnPrintTaskRequested;
        printDocument.Paginate += OnPaginate;
        printDocument.GetPreviewPage += OnGetPreviewPage;
        printDocument.AddPages += OnAddPages;

        try
        {
            await PrintManagerInterop.ShowPrintUIForWindowAsync(windowHandle)
                .AsTask()
                .ConfigureAwait(true);
        }
        finally
        {
            printManager.PrintTaskRequested -= OnPrintTaskRequested;
            printDocument.Paginate -= OnPaginate;
            printDocument.GetPreviewPage -= OnGetPreviewPage;
            printDocument.AddPages -= OnAddPages;
        }
    }

    private void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
    {
        args.Request.CreatePrintTask(
            "PrintSink WinRT E2E Source",
            sourceRequestedArgs => sourceRequestedArgs.SetSource(documentSource));
    }

    private void OnPaginate(object sender, PaginateEventArgs args)
    {
        page = CreatePage(sourceText);
        printDocument.SetPreviewPageCount(1, PreviewPageCountType.Final);
    }

    private void OnGetPreviewPage(object sender, GetPreviewPageEventArgs args)
    {
        printDocument.SetPreviewPage(args.PageNumber, page ?? CreatePage(sourceText));
    }

    private void OnAddPages(object sender, AddPagesEventArgs args)
    {
        printDocument.AddPage(page ?? CreatePage(sourceText));
        printDocument.AddPagesComplete();
    }

    private static XamlControls.Border CreatePage(string text)
    {
        XamlControls.TextBlock body = new()
        {
            Text = text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 32,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Colors.Black),
        };

        XamlControls.Border root = new()
        {
            Width = PageWidth,
            Height = PageHeight,
            Padding = new Thickness(96),
            Background = new SolidColorBrush(Colors.White),
            Child = body,
        };

        Size pageSize = new(PageWidth, PageHeight);
        root.Measure(pageSize);
        root.Arrange(new Rect(new Point(0, 0), pageSize));
        root.UpdateLayout();
        return root;
    }
}
