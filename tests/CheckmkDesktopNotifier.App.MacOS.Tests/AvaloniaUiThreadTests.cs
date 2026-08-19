using CheckmkDesktopNotifier.App.MacOS;

namespace CheckmkDesktopNotifier.App.MacOS.Tests;

public sealed class AvaloniaUiThreadTests
{
    [Fact]
    public void PostDeferred_runs_inline_when_dispatcher_is_unavailable()
    {
        var ui = new AvaloniaUiThread(() => null);
        var ran = false;
        ui.PostDeferred(() => ran = true);
        Assert.True(ran);
    }

    [Fact]
    public void Post_runs_inline_when_dispatcher_is_unavailable()
    {
        var ui = new AvaloniaUiThread(() => null);
        var ran = false;
        ui.Post(() => ran = true);
        Assert.True(ran);
        Assert.True(ui.CheckAccess());
    }
}
