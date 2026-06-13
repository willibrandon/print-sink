using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PrintSink.Core.Diagnostics;

/// <summary>
/// Stores recent PrintSink diagnostic events as local JSON.
/// </summary>
public sealed class LocalDiagnosticEventStore : IDiagnosticEventStore, IDisposable
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
    private readonly string semaphoreName;

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
        semaphoreName = CreateSemaphoreName(rootDirectory);
    }

    /// <inheritdoc />
    public async Task AppendAsync(
        DiagnosticEventRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        using Semaphore semaphore = new(1, 1, semaphoreName);
        bool semaphoreAcquired = false;
        try
        {
            semaphoreAcquired = WaitForSemaphore(semaphore);
            if (!semaphoreAcquired)
            {
                throw new TimeoutException("Timed out waiting for the diagnostics event store lock.");
            }

            Directory.CreateDirectory(rootDirectory);
            string path = GetEventsPath(rootDirectory);
            List<DiagnosticEventRecord> records = await ReadAllUnsafeAsync(path, cancellationToken)
                .ConfigureAwait(false);

            records.Add(record);
            if (records.Count > maximumStoredEvents)
            {
                records.RemoveRange(0, records.Count - maximumStoredEvents);
            }

            FileStream output = File.Create(path);
            await using (output.ConfigureAwait(false))
            {
                await JsonSerializer
                    .SerializeAsync(output, records, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            if (semaphoreAcquired)
            {
                semaphore.Release();
            }

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
        using Semaphore semaphore = new(1, 1, semaphoreName);
        bool semaphoreAcquired = false;
        try
        {
            semaphoreAcquired = WaitForSemaphore(semaphore);
            if (!semaphoreAcquired)
            {
                throw new TimeoutException("Timed out waiting for the diagnostics event store lock.");
            }

            string path = GetEventsPath(rootDirectory);
            List<DiagnosticEventRecord> records = await ReadAllUnsafeAsync(path, cancellationToken)
                .ConfigureAwait(false);

            records.Reverse();
            return [.. records.Take(maxCount)];
        }
        finally
        {
            if (semaphoreAcquired)
            {
                semaphore.Release();
            }

            gate.Release();
        }
    }

    private static bool WaitForSemaphore(Semaphore semaphore)
    {
        return semaphore.WaitOne(TimeSpan.FromSeconds(10));
    }

    private static async Task<List<DiagnosticEventRecord>> ReadAllUnsafeAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        FileStream input = File.OpenRead(path);
        List<DiagnosticEventRecord>? records;
        await using (input.ConfigureAwait(false))
        {
            records = await JsonSerializer
                .DeserializeAsync<List<DiagnosticEventRecord>>(input, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        return records ?? [];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        gate.Dispose();
    }

    private static string GetEventsPath(string rootDirectory)
    {
        return Path.Combine(rootDirectory, EventsFileName);
    }

    private static string CreateSemaphoreName(string rootDirectory)
    {
        string normalizedPath = Path.GetFullPath(rootDirectory).ToUpperInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));
        return $@"Local\PrintSink.Diagnostics.{Convert.ToHexString(hash)}";
    }
}
