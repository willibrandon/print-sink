using System.Text.Json;

namespace PrintSink.Core.Diagnostics;

/// <summary>
/// Stores recent PrintSink diagnostic events as local JSON.
/// </summary>
public sealed class LocalDiagnosticEventStore : IDiagnosticEventStore
{
    private const string EventsFileName = "diagnostic-events.json";
    private const int DefaultMaximumStoredEvents = 200;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string rootDirectory;
    private readonly int maximumStoredEvents;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDiagnosticEventStore"/> class.
    /// </summary>
    /// <param name="rootDirectory">The directory where diagnostic events are stored.</param>
    public LocalDiagnosticEventStore(string rootDirectory)
        : this(rootDirectory, DefaultMaximumStoredEvents)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDiagnosticEventStore"/> class.
    /// </summary>
    /// <param name="rootDirectory">The directory where diagnostic events are stored.</param>
    /// <param name="maximumStoredEvents">The maximum number of events retained on disk.</param>
    public LocalDiagnosticEventStore(string rootDirectory, int maximumStoredEvents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumStoredEvents);

        this.rootDirectory = rootDirectory;
        this.maximumStoredEvents = maximumStoredEvents;
    }

    /// <inheritdoc />
    public async Task AppendAsync(
        DiagnosticEventRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(rootDirectory);
            string path = GetEventsPath(rootDirectory);
            List<DiagnosticEventRecord> records = await ReadAllUnsafeAsync(path, cancellationToken)
                .ConfigureAwait(false);

            records.Add(record);
            if (records.Count > maximumStoredEvents)
            {
                records.RemoveRange(0, records.Count - maximumStoredEvents);
            }

            await using FileStream output = File.Create(path);
            await JsonSerializer
                .SerializeAsync(output, records, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiagnosticEventRecord>> ReadRecentAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string path = GetEventsPath(rootDirectory);
            List<DiagnosticEventRecord> records = await ReadAllUnsafeAsync(path, cancellationToken)
                .ConfigureAwait(false);

            records.Reverse();
            return [.. records.Take(maxCount)];
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<List<DiagnosticEventRecord>> ReadAllUnsafeAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        await using FileStream input = File.OpenRead(path);
        List<DiagnosticEventRecord>? records = await JsonSerializer
            .DeserializeAsync<List<DiagnosticEventRecord>>(input, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return records ?? [];
    }

    private static string GetEventsPath(string rootDirectory)
    {
        return Path.Combine(rootDirectory, EventsFileName);
    }
}
