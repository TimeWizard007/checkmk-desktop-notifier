using CheckmkDesktopNotifier.App.MacOS;

namespace CheckmkDesktopNotifier.App.MacOS.Tests;

public sealed class MacSingleInstanceToggleTests
{
    [Fact]
    public void GetOrCreate_does_not_allocate_a_second_instance()
    {
        var toggle = new MacSingleInstanceToggle<string>();
        var first = toggle.GetOrCreate(() => "panel");
        var second = toggle.GetOrCreate(() => "other");
        Assert.Equal(1, toggle.CreateCount);
        Assert.Same(first, second);
    }
}
