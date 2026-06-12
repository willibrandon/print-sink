using PrintSink.Core.Watermark;
using Windows.Graphics.Printing.Workflow;
using Windows.Storage.Streams;

namespace PrintSink.Xps.Projections;

/// <summary>
/// Wraps the generated native XPS page watermarker projection.
/// </summary>
public sealed class NativeXpsPageWatermarker
{
    private const int BufferSize = 81920;
    private const ulong ErrorNotImplemented = 0xFFFFFFFF80004001;

    private readonly PrintSink.Xps.XpsPageWatermarker watermarker;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeXpsPageWatermarker"/> class.
    /// </summary>
    public NativeXpsPageWatermarker()
    {
        using NativeXpsActivationContext activationContext = NativeXpsActivationContext.Activate();
        watermarker = new PrintSink.Xps.XpsPageWatermarker();
    }

    /// <summary>
    /// Applies text watermark options to the native watermarker.
    /// </summary>
    /// <param name="text">The text watermark options.</param>
    public void ApplyText(TextWatermark text)
    {
        ArgumentNullException.ThrowIfNull(text);

        watermarker.Text = text.Text;
        watermarker.FontFamily = text.FontFamily;
        watermarker.FontSize = text.FontSize;
        watermarker.Opacity = text.Opacity;
        watermarker.RotationDegrees = text.RotationDegrees;
        watermarker.OffsetX = text.OffsetX;
        watermarker.OffsetY = text.OffsetY;
    }

    /// <summary>
    /// Applies image watermark options to the native watermarker.
    /// </summary>
    /// <param name="image">The image watermark options.</param>
    public void ApplyImage(ImageWatermark image)
    {
        ArgumentNullException.ThrowIfNull(image);

        watermarker.ImagePath = image.ImagePath;
        watermarker.ImageWidth = image.Width;
        watermarker.ImageHeight = image.Height;
        watermarker.ImageOpacity = image.Opacity;
        watermarker.ImageRotationDegrees = image.RotationDegrees;
        watermarker.ImageOffsetX = image.OffsetX;
        watermarker.ImageOffsetY = image.OffsetY;
    }

    /// <summary>
    /// Applies the configured watermark to an XPS-family stream.
    /// </summary>
    /// <param name="source">The source XPS stream.</param>
    /// <param name="cancellationToken">A token that cancels stream copying.</param>
    /// <returns>The watermarked XPS stream.</returns>
    public async Task<Stream> ApplyAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        using InMemoryRandomAccessStream input = new();
        using (IOutputStream output = input.GetOutputStreamAt(0))
        {
            await WriteFromStreamAsync(source, output, cancellationToken).ConfigureAwait(false);
        }

        input.Seek(0);
        MemoryStream? objectModelResult = await TryApplyWithObjectModelAsync(input, cancellationToken).ConfigureAwait(false);
        if (objectModelResult is not null)
        {
            return objectModelResult;
        }

        input.Seek(0);
        using IRandomAccessStream watermarked = watermarker.ApplyToPackage(input);
        using IInputStream watermarkedInput = watermarked.GetInputStreamAt(0);
        return await ReadToMemoryAsync(watermarkedInput, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MemoryStream?> TryApplyWithObjectModelAsync(
        IRandomAccessStream input,
        CancellationToken cancellationToken)
    {
        PrintWorkflowObjectModelSourceFileContent sourceContent = new(input.GetInputStreamAt(0));
        PrintSink.Xps.XpsSequentialDocument document = new(sourceContent);
        ulong? generationFailure = null;
        document.XpsGenerationFailed += (_, error) => generationFailure = error;

        using IInputStream watermarkedInput = document.GetWatermarkedStream(watermarker);
        MemoryStream result = await ReadToMemoryAsync(watermarkedInput, cancellationToken).ConfigureAwait(false);
        if (generationFailure is not null)
        {
            if (generationFailure.Value == ErrorNotImplemented)
            {
                result.Dispose();
                return null;
            }

            throw new InvalidOperationException(
                $"XPS object model generation failed with HRESULT 0x{generationFailure.Value:X8}.");
        }

        if (result.Length == 0)
        {
            throw new InvalidOperationException("XPS object model generation produced no output.");
        }

        return result;
    }

    private static async Task<MemoryStream> ReadToMemoryAsync(
        IInputStream input,
        CancellationToken cancellationToken)
    {
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

    private static async Task WriteFromStreamAsync(
        Stream source,
        IOutputStream output,
        CancellationToken cancellationToken)
    {
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
