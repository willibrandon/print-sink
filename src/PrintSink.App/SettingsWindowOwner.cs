using Microsoft.UI.Reactor;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using Windows.Graphics.Printing.PrintSupport;
using WinRT.Interop;

namespace PrintSink.App;

internal static partial class SettingsWindowOwner
{
    private const int GwlParentWindow = -8;

    internal static string Apply(ReactorWindow? window, PrintSupportSettingsActivatedEventArgs? settingsArgs)
    {
        if (window is null)
        {
            return "Settings window unavailable.";
        }

        if (settingsArgs is null)
        {
            return "Owner window unavailable.";
        }

        nint ownerHwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(ToMicrosoftWindowId(settingsArgs.OwnerWindowId));
        if (ownerHwnd == 0)
        {
            return "Owner HWND unavailable.";
        }

        nint childHwnd = WindowNative.GetWindowHandle(window.NativeWindow);
        if (childHwnd == 0)
        {
            return "Settings HWND unavailable.";
        }

        _ = SetWindowLongPtr(childHwnd, GwlParentWindow, ownerHwnd);
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsModal = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            return "Modal to print preferences owner.";
        }

        return "Owned by print preferences window.";
    }

    internal static Microsoft.UI.WindowId ToMicrosoftWindowId(Windows.UI.WindowId windowId)
    {
        Microsoft.UI.WindowId result;
        result.Value = windowId.Value;
        return result;
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr(nint windowHandle, int index, nint newLong);
}
