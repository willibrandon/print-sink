using System.Text.Json;

namespace PrintSink.Core.Diagnostics;

/// <summary>
/// Stores recent PrintSink diagnostic events as local JSON.
/// </summary>
public sealed class LocalDiagnosticEventStore : IDiagnosticEventStore, IDisposable
{
    private const string EventsFileName = "diagnostic-events.json";
    private const string LockFileName = "diagnostic-events.lock";
    private const int DefaultMaximumStoredEvents = 4096;
    private const int FileBufferSize = 4096;
    private const int TransientFileRetryCount = 40;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private static readonly TimeSpan TransientFileRetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string rootDirectory;
    private readonly int maximumStoredEvents;
    private readonly string lockFilePath;

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
        lockFilePath = GetLockPath(rootDirectory);
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
            FileStream lockFile = await AcquireLockFileAsync(lockFilePath, cancellationToken)
                .ConfigureAwait(false);
            await using (lockFile.ConfigureAwait(false))
            {
                string path = GetEventsPath(rootDirectory);
                List<DiagnosticEventRecord> records = await ReadAllUnsafeAsync(path, cancellationToken)
                    .ConfigureAwait(false);

                records.Add(record);
                if (records.Count > maximumStoredEvents)
                {
                    records.RemoveRange(0, records.Count - maximumStoredEvents);
                }

                await WriteAllUnsafeAsync(path, records, cancellationToken).ConfigureAwait(false);
            }
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
            Directory.CreateDirectory(rootDirectory);
            FileStream lockFile = await AcquireLockFileAsync(lockFilePath, cancellationToken)
                .ConfigureAwait(false);
            await using (lockFile.ConfigureAwait(false))
            {
                string path = GetEventsPath(rootDirectory);
                List<DiagnosticEventRecord> records = await ReadAllUnsafeAsync(path, cancellationToken)
                    .ConfigureAwait(false);

                records.Reverse();
                return [.. records.Take(maxCount)];
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<FileStream> AcquireLockFileAsync(string path, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    path,
                    new FileStreamOptions
                    {
                        Mode = FileMode.OpenOrCreate,
                        Access = FileAccess.ReadWrite,
                        Share = FileShare.None,
                        BufferSize = 1,
                        Options = FileOptions.Asynchronous,
                    });
            }
            catch (Exception ex) when (IsTransientFileAccessFailure(ex) && attempt < TransientFileRetryCount)
            {
                await Task.Delay(TransientFileRetryDelay, cancellationToken).ConfigureAwait(false);
            }
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

        try
        {
            return await UseFileWithTransientRetryAsync(
                    () => CreateEventsFileReadStream(path),
                    async input =>
                    {
                        List<DiagnosticEventRecord>? records = await JsonSerializer
                            .DeserializeAsync<List<DiagnosticEventRecord>>(input, SerializerOptions, cancellationToken)
                            .ConfigureAwait(false);
                        return records ?? [];
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return [];
        }
    }

    private static async Task WriteAllUnsafeAsync(
        string path,
        IReadOnlyList<DiagnosticEventRecord> records,
        CancellationToken cancellationToken)
    {
        string tempPath = CreateTemporaryEventsPath(path);
        try
        {
            await UseFileWithTransientRetryAsync(
                    () => CreateEventsFileWriteStream(tempPath),
                    async output =>
                    {
                        await JsonSerializer
                            .SerializeAsync(output, records, SerializerOptions, cancellationToken)
                            .ConfigureAwait(false);
                        return true;
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            await ReplaceEventsFileAsync(tempPath, path, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static FileStream CreateEventsFileReadStream(string path)
    {
        return new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                BufferSize = FileBufferSize,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
    }

    private static FileStream CreateEventsFileWriteStream(string path)
    {
        return new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.Read,
                BufferSize = FileBufferSize,
                Options = FileOptions.Asynchronous,
            });
    }

    private static async Task ReplaceEventsFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(destinationPath))
                {
                    File.Replace(sourcePath, destinationPath, null);
                }
                else
                {
                    File.Move(sourcePath, destinationPath);
                }

                return;
            }
            catch (Exception ex) when (IsTransientFileAccessFailure(ex) && attempt < TransientFileRetryCount)
            {
                await Task.Delay(TransientFileRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<TResult> UseFileWithTransientRetryAsync<TResult>(
        Func<FileStream> openFile,
        Func<FileStream, Task<TResult>> useFile,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                FileStream? file = null;
                try
                {
                    file = openFile();
                    return await useFile(file).ConfigureAwait(false);
                }
                finally
                {
                    if (file is not null)
                    {
                        await file.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex) when (
                ex is not FileNotFoundException
                && ex is not JsonException
                && IsTransientFileAccessFailure(ex)
                && attempt < TransientFileRetryCount)
            {
                await Task.Delay(TransientFileRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransientFileAccessFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    private static string CreateTemporaryEventsPath(string path)
    {
        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Diagnostic event path has no directory: {path}");
        string fileName = Path.GetFileName(path);
        return Path.Combine(directory, $"{fileName}.{Guid.NewGuid():N}.tmp");
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

    private static string GetLockPath(string rootDirectory)
    {
        return Path.Combine(rootDirectory, LockFileName);
    }
}
