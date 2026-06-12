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

        await using FileStream input = File.OpenRead(path);
        WatermarkOptions? options = await JsonSerializer
            .DeserializeAsync<WatermarkOptions>(input, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

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
        await using FileStream output = File.Create(path);
        await JsonSerializer
            .SerializeAsync(output, options, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string GetWatermarkPath(string rootDirectory, Uri printerUri)
    {
        string normalizedUri = printerUri.AbsoluteUri.ToUpperInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedUri));
        return Path.Combine(rootDirectory, $"{Convert.ToHexString(hash)}.watermark.json");
    }
}
