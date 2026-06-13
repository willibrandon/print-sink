using PrintSink.E2E.IppPrinter;

IppPrinterOptions options = IppPrinterOptions.Parse(args);
Directory.CreateDirectory(options.OutputDirectory);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = [],
});
builder.Logging.ClearProviders();
builder.WebHost.UseUrls($"http://127.0.0.1:{options.Port}");
builder.WebHost.ConfigureKestrel(kestrelOptions => kestrelOptions.AllowSynchronousIO = true);

IppPrinterServer printerServer = new(options);
WebApplication app = builder.Build();

app.Use(async (context, next) =>
{
    LogHttpRequest(options, context);
    await next(context).ConfigureAwait(false);
});

app.MapGet("/", () => Results.Text("PrintSink E2E IPP printer"));
app.MapGet("/{**path}", () => Results.Text("PrintSink E2E IPP printer"));
app.MapPost("/{**path}", async context =>
{
    context.Response.ContentType = "application/ipp";
    await printerServer
        .ProcessAsync(context.Request.Body, context.Response.Body, context.RequestAborted)
        .ConfigureAwait(false);
});

await app.StartAsync().ConfigureAwait(false);
if (!string.IsNullOrWhiteSpace(options.ReadyFile))
{
    Directory.CreateDirectory(Path.GetDirectoryName(options.ReadyFile)!);
    await File.WriteAllTextAsync(options.ReadyFile, DateTimeOffset.UtcNow.ToString("O")).ConfigureAwait(false);
}

await app.WaitForShutdownAsync().ConfigureAwait(false);

static void LogHttpRequest(IppPrinterOptions options, HttpContext context)
{
    string line = string.Join(
        " ",
        DateTimeOffset.UtcNow.ToString("O"),
        context.Request.Method,
        context.Request.Scheme,
        context.Request.Host.Value,
        context.Request.Path.Value,
        context.Request.ContentType ?? "<no-content-type>");
    File.AppendAllText(options.HttpRequestLogPath, line + Environment.NewLine);
}
