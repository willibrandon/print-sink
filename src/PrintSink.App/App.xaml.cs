using Microsoft.UI.Xaml;

namespace PrintSink;

/// <summary>
/// Provides application-specific behavior to supplement the default application class.
/// </summary>
public partial class App : Application
{
    private Window? window;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new MainWindow();
        window.Activate();
    }
}
