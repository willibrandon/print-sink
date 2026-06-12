extern alias PrintSinkApp;

using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace PrintSink.App.Tests;

/// <summary>
/// Tests package-hosted app behavior.
/// </summary>
[TestClass]
public sealed class AppPackageTests
{
    /// <summary>
    /// Verifies the management activation route exposes the shell metadata used at normal launch.
    /// </summary>
    [TestMethod]
    public void Management_route_uses_management_shell_metadata()
    {
        PrintSinkApp::PrintSink.App.AppActivationRoute route =
            PrintSinkApp::PrintSink.App.AppActivationRoute.Management(42);

        Assert.AreEqual(42, route.ActivationId);
        Assert.AreEqual(PrintSinkApp::PrintSink.App.AppActivationRouteKind.Management, route.Kind);
        Assert.AreEqual("PrintSink", route.Title);
        Assert.AreEqual("Virtual printer management", route.Subtitle);
        Assert.IsNull(route.SettingsArgs);
        Assert.IsNull(route.JobArgs);
    }

    /// <summary>
    /// Verifies the packaged test host owns a usable XAML UI thread.
    /// </summary>
    [UITestMethod]
    public void Xaml_runtime_is_available_inside_packaged_test_host()
    {
        Grid grid = new();

        Assert.AreEqual(0, grid.MinWidth);
    }
}
