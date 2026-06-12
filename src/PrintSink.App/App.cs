using Microsoft.UI.Reactor;
using Microsoft.Windows.AppLifecycle;
using PrintSink.App;

AppActivationState.SetRoute(AppActivationRouter.GetCurrentRoute());
AppInstance.GetCurrent().Activated += (_, args) =>
{
    AppActivationState.SetRoute(AppActivationRouter.From(args));
};

ReactorApp.Run<AppRoot>("PrintSink", width: 1040, height: 720);
