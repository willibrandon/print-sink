using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Windows.Graphics.Printing.PrintSupport;
using static Microsoft.UI.Reactor.Factories;

namespace PrintSink.App;

/// <summary>
/// Shows the Print Support settings activation surface.
/// </summary>
internal sealed class SettingsScreen : Component<AppActivationRoute>
{
    /// <summary>
    /// Renders the settings activation screen.
    /// </summary>
    /// <returns>The settings screen element tree.</returns>
    public override Element Render()
    {
        PrintSupportSettingsActivatedEventArgs? settingsArgs = Props.SettingsArgs;
        string launchKind = settingsArgs?.Session.LaunchKind.ToString() ?? "Unavailable";
        string ownerWindowId = settingsArgs?.OwnerWindowId.Value.ToString() ?? "Unavailable";

        return ScrollView(
            VStack(20,
                TitleBlock("Print preferences", "Activated through the Print Support Settings UI contract."),
                CardSurface(
                    VStack(12,
                        TextBlock("Activation")
                            .ApplyStyle("SubtitleTextBlockStyle")
                            .Bold(),
                        DetailGrid(
                            ("Launch kind", launchKind),
                            ("Owner window", ownerWindowId),
                            ("Session", settingsArgs is null ? "Not available" : "Connected")))),
                CardSurface(
                    VStack(12,
                        TextBlock("Preferences")
                            .ApplyStyle("SubtitleTextBlockStyle")
                            .Bold(),
                        TextBlock("This session is isolated from the management dashboard and runs under the Print Support Settings UI contract.")
                            .Foreground(Theme.SecondaryText)
                            .Set(text => text.TextWrapping = TextWrapping.Wrap),
                        Button("Close", Microsoft.UI.Xaml.Application.Current.Exit))))
            .Padding(32)
            .MaxWidth(920)
            .HAlign(HorizontalAlignment.Center));
    }

    private static StackElement TitleBlock(string title, string subtitle)
    {
        return VStack(4,
            TextBlock(title)
                .ApplyStyle("TitleTextBlockStyle")
                .Bold(),
            TextBlock(subtitle)
                .Foreground(Theme.SecondaryText)
                .Set(text => text.TextWrapping = TextWrapping.Wrap));
    }

    private static GridElement DetailGrid(params (string Label, string Value)[] rows)
    {
        return Grid(
            columns: [GridSize.Px(140), GridSize.Star()],
            rows: [.. Enumerable.Repeat(GridSize.Auto, rows.Length)],
            [.. rows.SelectMany((row, index) => new Element[]
            {
                TextBlock(row.Label)
                    .Foreground(Theme.SecondaryText)
                    .Grid(row: index, column: 0),
                TextBlock(row.Value)
                    .Set(text => text.TextWrapping = TextWrapping.Wrap)
                    .Grid(row: index, column: 1),
            })]);
    }

    private static BorderElement CardSurface(Element content)
    {
        return Border(content)
            .Padding(16)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke)
            .CornerRadius(8);
    }
}
