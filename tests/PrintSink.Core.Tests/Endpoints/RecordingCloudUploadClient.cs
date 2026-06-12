using PrintSink.Endpoints;

namespace PrintSink.Core.Tests.Endpoints;

/// <summary>
/// Records cloud uploads for sink tests.
/// </summary>
public sealed class RecordingCloudUploadClient : ICloudUploadClient
{
    /// <summary>
    /// Gets the uploaded bytes.
    /// </summary>
    public byte[] Bytes { get; private set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets the uploaded job name.
    /// </summary>
    public string? JobName { get; private set; }

    /// <inheritdoc />
    public async Task UploadAsync(Stream pdlStream, SinkWriteContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdlStream);
        ArgumentNullException.ThrowIfNull(context);

        using MemoryStream buffer = new();
        await pdlStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        Bytes = buffer.ToArray();
        JobName = context.JobName;
    }
}
