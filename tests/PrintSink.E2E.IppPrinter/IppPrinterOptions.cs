namespace PrintSink.E2E.IppPrinter;

internal sealed class IppPrinterOptions
{
    internal string PrinterName { get; private set; } = "PrintSink E2E IPP";

    internal int Port { get; private set; } = 18631;

    internal string OutputDirectory { get; private set; } =
        Path.Combine(Path.GetTempPath(), "PrintSink.E2E.IppPrinter");

    internal string DocumentFormat { get; private set; } = "application/pdf";

    internal string? ReadyFile { get; private set; }

    internal Uri PrinterUri => new($"ipp://127.0.0.1:{Port}/ipp/printer/{Uri.EscapeDataString(PrinterName)}");

    internal string HttpRequestLogPath => Path.Combine(OutputDirectory, "http-requests.log");

    internal string ErrorLogPath => Path.Combine(OutputDirectory, "ipp-errors.log");

    internal static IppPrinterOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        IppPrinterOptions options = new();
        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--printer-name":
                    options.PrinterName = GetRequiredValue(args, ref index, argument);
                    break;
                case "--port":
                    options.Port = GetRequiredPort(args, ref index, argument);
                    break;
                case "--output":
                    options.OutputDirectory = GetRequiredValue(args, ref index, argument);
                    break;
                case "--document-format":
                    options.DocumentFormat = GetRequiredValue(args, ref index, argument);
                    break;
                case "--ready-file":
                    options.ReadyFile = GetRequiredValue(args, ref index, argument);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{argument}'.");
            }
        }

        return options;
    }

    private static string GetRequiredValue(IReadOnlyList<string> args, ref int index, string option)
    {
        if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new ArgumentException($"Missing value for {option}.");
        }

        index++;
        return args[index];
    }

    private static int GetRequiredPort(IReadOnlyList<string> args, ref int index, string option)
    {
        string value = GetRequiredValue(args, ref index, option);
        return int.TryParse(value, out int port) && port is > 0 and <= 65535
            ? port
            : throw new ArgumentException($"{option} must be a TCP port number.");
    }

}
