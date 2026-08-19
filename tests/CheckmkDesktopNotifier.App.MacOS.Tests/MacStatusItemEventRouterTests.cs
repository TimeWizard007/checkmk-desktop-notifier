using CheckmkDesktopNotifier.App.MacOS;
using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.App.MacOS.Tests;

public sealed class MacStatusItemEventRouterTests
{
    [Fact]
    public void Left_click_dispatches_toggle_through_ui_marshal_and_does_not_run_inline()
    {
        using var status = new NullMacStatusItem();
        var queued = new List<Action>();
        var toggles = 0;
        using var router = new MacStatusItemEventRouter(
            status,
            action => queued.Add(action),
            Commands(toggle: () => toggles++));

        status.RaiseActivated();

        Assert.Equal(1, router.PostedCount);
        Assert.Single(queued);
        Assert.Equal(0, toggles);

        queued[0]();
        Assert.Equal(1, toggles);
    }

    [Fact]
    public void Callback_exception_does_not_escape_native_event()
    {
        using var status = new NullMacStatusItem();
        Exception? logged = null;
        using var router = new MacStatusItemEventRouter(
            status,
            action => action(),
            Commands(toggle: () => throw new InvalidOperationException("Bearer secret-value")),
            ex => logged = ex);

        status.RaiseActivated();

        Assert.NotNull(logged);
        Assert.Equal("Bearer secret-value", logged!.Message);
    }

    [Fact]
    public void Repeated_toggle_reuses_one_panel()
    {
        using var status = new NullMacStatusItem();
        var queued = new List<Action>();
        var panels = new MacSingleInstanceToggle<object>();
        var visible = false;
        var shows = 0;
        var hides = 0;
        using var router = new MacStatusItemEventRouter(
            status,
            action => queued.Add(action),
            Commands(toggle: () =>
            {
                panels.GetOrCreate(() => new object());
                if (visible)
                {
                    visible = false;
                    hides++;
                    return;
                }

                visible = true;
                shows++;
            }));

        status.RaiseActivated();
        status.RaiseActivated();
        status.RaiseActivated();
        foreach (var action in queued)
        {
            action();
        }

        Assert.Equal(1, panels.CreateCount);
        Assert.Equal(2, shows);
        Assert.Equal(1, hides);
        Assert.True(visible);
    }

    private static MacStatusItemCommands Commands(Action toggle) =>
        new()
        {
            ToggleProblems = toggle,
            ShowProblems = () => { },
            ShowSettings = () => { },
            OpenCheckmk = () => { },
            Quit = () => { }
        };
}
