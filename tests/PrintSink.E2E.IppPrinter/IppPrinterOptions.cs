namespace PrintSink.E2E.IppPrinter;

using System.Reflection;
using SharpIpp.Protocol.Models;

internal sealed class IppPrinterOptions
{
    internal string PrinterName { get; private set; } = "PrintSink E2E IPP";

    internal int Port { get; private set; } = 18631;

    internal string Host { get; private set; } = "127.0.0.1";

    internal string OutputDirectory { get; private set; } =
        Path.Combine(Path.GetTempPath(), "PrintSink.E2E.IppPrinter");

    internal string DocumentFormat { get; private set; } = "application/pdf";

    internal PrinterState PrinterState { get; private set; } = PrinterState.Idle;

    internal PrinterStateReason[] PrinterStateReasons { get; private set; } = [PrinterStateReason.None];

    internal bool RejectJobs { get; private set; }

    internal TimeSpan ResponseDelay { get; private set; } = TimeSpan.Zero;

    internal string? ReadyFile { get; private set; }

    internal Uri PrinterUri => new($"ipp://{Host}:{Port}/ipp/printer/{Uri.EscapeDataString(PrinterName)}");

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
                case "--host":
                    options.Host = GetRequiredValue(args, ref index, argument);
                    break;
                case "--output":
                    options.OutputDirectory = GetRequiredValue(args, ref index, argument);
                    break;
                case "--document-format":
                    options.DocumentFormat = GetRequiredValue(args, ref index, argument);
                    break;
                case "--printer-state":
                    options.PrinterState = GetRequiredPrinterState(args, ref index, argument);
                    break;
                case "--printer-state-reason":
                    options.PrinterStateReasons = GetRequiredPrinterStateReasons(args, ref index, argument);
                    break;
                case "--reject-jobs":
                    options.RejectJobs = true;
                    break;
                case "--response-delay-ms":
                    options.ResponseDelay = TimeSpan.FromMilliseconds(GetRequiredNonNegativeInteger(args, ref index, argument));
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

    private static int GetRequiredNonNegativeInteger(IReadOnlyList<string> args, ref int index, string option)
    {
        string value = GetRequiredValue(args, ref index, option);
        return int.TryParse(value, out int result) && result >= 0
            ? result
            : throw new ArgumentException($"{option} must be a non-negative integer.");
    }

    private static PrinterState GetRequiredPrinterState(IReadOnlyList<string> args, ref int index, string option)
    {
        string value = GetRequiredValue(args, ref index, option);
        return Enum.TryParse(value, true, out PrinterState state)
            ? state
            : throw new ArgumentException($"{option} has unsupported printer state '{value}'.");
    }

    private static PrinterStateReason[] GetRequiredPrinterStateReasons(
        IReadOnlyList<string> args,
        ref int index,
        string option)
    {
        string value = GetRequiredValue(args, ref index, option);
        PrinterStateReason[] reasons =
        [
            .. value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParsePrinterStateReason),
        ];

        return reasons.Length == 0 ? [PrinterStateReason.None] : reasons;
    }

    private static PrinterStateReason ParsePrinterStateReason(string value)
    {
        foreach (FieldInfo field in typeof(PrinterStateReason).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not PrinterStateReason reason)
            {
                continue;
            }

            if (string.Equals(field.Name, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                return reason;
            }
        }

        throw new ArgumentException($"Unsupported printer-state-reason '{value}'.");
    }
}
