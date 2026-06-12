using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

ReactorApp.Run<App>("PrintSink.App", width: 900, height: 600);

class App : Component
{
    public override Element Render()
    {
        var (name, setName) = UseState("World");

        var titleBar = TitleBar("PrintSink.App").Flex(shrink: 0);

        var body = Border(
            FlexColumn(
                Heading($"Hello, {name}!"),
                TextBox(name, setName, placeholderText: "Your name")
                    .AutomationName("NameInput")
            ) with { RowGap = 16 }
        ).Padding(24).Flex(grow: 1, basis: 0);

        return FlexColumn(titleBar, body)
            .Backdrop(BackdropKind.Mica);
    }
}
