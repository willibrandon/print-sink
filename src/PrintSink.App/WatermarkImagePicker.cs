using Windows.Storage;
using Windows.Storage.Pickers;

namespace PrintSink.App;

/// <summary>
/// Opens image files for watermark configuration from a packaged WinUI window.
/// </summary>
internal static class WatermarkImagePicker
{
    /// <summary>
    /// Opens the image picker for the supplied owner window.
    /// </summary>
    /// <param name="window">The owner window for picker modality.</param>
    /// <returns>The selected image file, or <see langword="null" /> when the picker is canceled.</returns>
    internal static async Task<StorageFile?> PickAsync(Microsoft.UI.Xaml.Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            CommitButtonText = "Use image",
        };
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");

        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);

        return await picker.PickSingleFileAsync().AsTask().ConfigureAwait(false);
    }
}
