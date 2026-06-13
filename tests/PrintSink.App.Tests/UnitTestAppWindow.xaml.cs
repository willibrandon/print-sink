using Microsoft.UI.Xaml;

namespace PrintSink.App.Tests;

/// <summary>
/// Hosts the packaged WinUI test runner window.
/// </summary>
internal sealed class UnitTestAppWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestAppWindow"/> class.
    /// </summary>
    internal UnitTestAppWindow()
    {
        Title = "PrintSink.App.Tests";
    }
}
