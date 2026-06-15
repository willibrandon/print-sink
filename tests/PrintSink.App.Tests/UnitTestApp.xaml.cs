using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace PrintSink.App.Tests;

/// <summary>
/// Provides the packaged WinUI test application host.
/// </summary>
internal sealed partial class UnitTestApp : Application
{
    private Window? window;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestApp"/> class.
    /// </summary>
    internal UnitTestApp()
    {
    }

    /// <summary>
    /// Invoked when the packaged test application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Microsoft.VisualStudio.TestPlatform.TestExecutor.UnitTestClient.CreateDefaultUI();

        window = new UnitTestAppWindow();
        window.Activate();

        UITestMethodAttribute.DispatcherQueue = window.DispatcherQueue;

        Microsoft.VisualStudio.TestPlatform.TestExecutor.UnitTestClient.Run(Environment.CommandLine);
    }
}
