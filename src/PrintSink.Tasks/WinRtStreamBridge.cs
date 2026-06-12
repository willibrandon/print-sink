using Windows.Storage.Streams;

namespace PrintSink.Tasks;

internal static class WinRtStreamBridge
{
    private const int BufferSize = 81920;

    internal static async Task<MemoryStream> ReadToMemoryAsync(
        IInputStream input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        MemoryStream result = new();
        using DataReader reader = new(input)
        {
            InputStreamOptions = InputStreamOptions.Partial,
        };

        while (true)
        {
            uint loaded = await reader.LoadAsync(BufferSize).AsTask(cancellationToken).ConfigureAwait(false);
            if (loaded == 0)
            {
                break;
            }

            byte[] bytes = new byte[checked((int)loaded)];
            reader.ReadBytes(bytes);
            await result.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }

        result.Position = 0;
        return result;
    }

    internal static async Task WriteFromStreamAsync(
        Stream source,
        IOutputStream output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);

        using DataWriter writer = new(output);
        byte[] buffer = new byte[BufferSize];
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (read == buffer.Length)
            {
                writer.WriteBytes(buffer);
            }
            else
            {
                byte[] slice = new byte[read];
                System.Buffer.BlockCopy(buffer, 0, slice, 0, read);
                writer.WriteBytes(slice);
            }

            await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
        writer.DetachStream();
    }
}
