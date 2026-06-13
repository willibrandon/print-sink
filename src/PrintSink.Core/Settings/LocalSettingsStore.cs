using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PrintSink.Core.Watermark;

namespace PrintSink.Core.Settings;

/// <summary>
/// Persists PrintSink settings as JSON files under a caller-provided directory.
/// </summary>
public sealed class LocalSettingsStore : ISettingsStore
{
    private const string JobUiOptionsFileName = "job-ui-options.json";
    private const string PendingJobOptionsFileName = "pending-job-options.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string rootDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalSettingsStore"/> class.
    /// </summary>
    /// <param name="rootDirectory">The directory where settings files are stored.</param>
    public LocalSettingsStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        this.rootDirectory = rootDirectory;
    }

    /// <inheritdoc />
    public async Task<WatermarkOptions> GetWatermarkOptionsAsync(Uri printerUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(printerUri);

        string path = GetWatermarkPath(rootDirectory, printerUri);
        if (!File.Exists(path))
        {
            return WatermarkOptions.Disabled;
        }

        FileStream input = File.OpenRead(path);
        WatermarkOptions? options;
        await using (input.ConfigureAwait(false))
        {
            options = await JsonSerializer
                .DeserializeAsync<WatermarkOptions>(input, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        return options ?? WatermarkOptions.Disabled;
    }

    /// <inheritdoc />
    public async Task SaveWatermarkOptionsAsync(
        Uri printerUri,
        WatermarkOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(printerUri);
        ArgumentNullException.ThrowIfNull(options);

        Directory.CreateDirectory(rootDirectory);

        string path = GetWatermarkPath(rootDirectory, printerUri);
        FileStream output = File.Create(path);
        await using (output.ConfigureAwait(false))
        {
            await JsonSerializer
                .SerializeAsync(output, options, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<JobUiOptions> GetJobUiOptionsAsync(CancellationToken cancellationToken = default)
    {
        string path = GetJobUiOptionsPath(rootDirectory);
        if (!File.Exists(path))
        {
            return JobUiOptions.Default;
        }

        FileStream input = File.OpenRead(path);
        JobUiOptions? options;
        await using (input.ConfigureAwait(false))
        {
            options = await JsonSerializer
                .DeserializeAsync<JobUiOptions>(input, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        return options ?? JobUiOptions.Default;
    }

    /// <inheritdoc />
    public async Task SaveJobUiOptionsAsync(
        JobUiOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        Directory.CreateDirectory(rootDirectory);

        string path = GetJobUiOptionsPath(rootDirectory);
        FileStream output = File.Create(path);
        await using (output.ConfigureAwait(false))
        {
            await JsonSerializer
                .SerializeAsync(output, options, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task SaveJobProcessingOptionsAsync(
        JobProcessingOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        Directory.CreateDirectory(rootDirectory);

        string path = GetJobProcessingOptionsPath(rootDirectory);
        FileStream output = File.Create(path);
        await using (output.ConfigureAwait(false))
        {
            await JsonSerializer
                .SerializeAsync(output, options, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<JobProcessingOptions?> ConsumeJobProcessingOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        string path = GetJobProcessingOptionsPath(rootDirectory);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            JobProcessingOptions? options;
            FileStream input = File.OpenRead(path);
            await using (input.ConfigureAwait(false))
            {
                options = await JsonSerializer
                    .DeserializeAsync<JobProcessingOptions>(input, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Delete(path);
            return options;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static string GetWatermarkPath(string rootDirectory, Uri printerUri)
    {
        string normalizedUri = printerUri.AbsoluteUri.ToUpperInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedUri));
        return Path.Combine(rootDirectory, $"{Convert.ToHexString(hash)}.watermark.json");
    }

    private static string GetJobProcessingOptionsPath(string rootDirectory)
    {
        return Path.Combine(rootDirectory, PendingJobOptionsFileName);
    }

    private static string GetJobUiOptionsPath(string rootDirectory)
    {
        return Path.Combine(rootDirectory, JobUiOptionsFileName);
    }
}
