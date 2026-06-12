using Microsoft.UI.Xaml;

namespace PrintSink.App.Tests;

/// <summary>
/// Hosts the packaged WinUI test runner window.
/// </summary>
public sealed partial class UnitTestAppWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestAppWindow"/> class.
    /// </summary>
    public UnitTestAppWindow()
    {
        InitializeComponent();
    }
}
