using PrintSink.Core.Settings;
using PrintSink.Core.Watermark;

namespace PrintSink.Core.Tests.Processing;

internal sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly Dictionary<Uri, WatermarkOptions> watermarkOptions = [];

    /// <inheritdoc />
    public Task<WatermarkOptions> GetWatermarkOptionsAsync(
        Uri printerUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(printerUri);

        return Task.FromResult(watermarkOptions.GetValueOrDefault(printerUri, WatermarkOptions.Disabled));
    }

    /// <inheritdoc />
    public Task SaveWatermarkOptionsAsync(
        Uri printerUri,
        WatermarkOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(printerUri);
        ArgumentNullException.ThrowIfNull(options);

        watermarkOptions[printerUri] = options;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SaveJobProcessingOptionsAsync(
        JobProcessingOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<JobProcessingOptions?> ConsumeJobProcessingOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<JobProcessingOptions?>(null);
    }
}
