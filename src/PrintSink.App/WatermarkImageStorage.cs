using System.Security.Cryptography;
using PrintSink.Core.Watermark;

namespace PrintSink.App;

/// <summary>
/// Imports watermark images into package-local storage for background task access.
/// </summary>
internal static class WatermarkImageStorage
{
    private const string WatermarkImagesDirectoryName = "WatermarkImages";

    /// <summary>
    /// Creates an image watermark that points at a package-local image copy.
    /// </summary>
    /// <param name="sourcePath">The source image path selected or supplied by the user.</param>
    /// <param name="width">The image width in DIPs.</param>
    /// <param name="height">The image height in DIPs.</param>
    /// <param name="opacity">The opacity from 0.0 through 1.0.</param>
    /// <param name="rotationDegrees">The clockwise rotation in degrees.</param>
    /// <param name="offsetX">The horizontal offset in DIPs.</param>
    /// <param name="offsetY">The vertical offset in DIPs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The image watermark that references package-local storage.</returns>
    internal static async Task<ImageWatermark> CreateImageWatermarkAsync(
        string sourcePath,
        double width,
        double height,
        double opacity,
        double rotationDegrees,
        double offsetX,
        double offsetY,
        CancellationToken cancellationToken = default)
    {
        string importedPath = await ImportAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        return new ImageWatermark(importedPath, width, height, opacity, rotationDegrees, offsetX, offsetY);
    }

    private static async Task<string> ImportAsync(string sourcePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        string fullSourcePath = Path.GetFullPath(sourcePath.Trim());
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException("Watermark image file was not found.", fullSourcePath);
        }

        string destinationDirectory = Path.Combine(AppSettingsStoreFactory.GetRootDirectory(), WatermarkImagesDirectoryName);
        Directory.CreateDirectory(destinationDirectory);

        FileStream input = File.OpenRead(fullSourcePath);
        await using (input.ConfigureAwait(false))
        {
            byte[] hash = await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false);
            string extension = Path.GetExtension(fullSourcePath);
            string destinationPath = Path.Combine(destinationDirectory, $"{Convert.ToHexString(hash)}{extension}");

            if (!File.Exists(destinationPath))
            {
                input.Position = 0;
                FileStream output = File.Create(destinationPath);
                await using (output.ConfigureAwait(false))
                {
                    await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                }
            }

            return destinationPath;
        }
    }
}
