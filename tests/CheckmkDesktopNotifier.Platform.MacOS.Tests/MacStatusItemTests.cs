using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.Platform.MacOS.Tests;

public sealed class MacStatusItemTests
{
    [Fact]
    public void Factory_does_not_use_windows_status_apis_off_macos()
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }

        using var item = MacStatusItemFactory.Create();
        var nullItem = Assert.IsType<NullMacStatusItem>(item);
        nullItem.SetTitle("N:1 C:2 W:0");
        nullItem.SetToolTip("Connected");
        Assert.Equal("N:1 C:2 W:0", nullItem.Title);
        Assert.Equal("Connected", nullItem.ToolTip);
        Assert.False(nullItem.TryGetAnchor(out _));
    }

    [Fact]
    public void Native_status_item_is_gated_off_macos()
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }

        Assert.Throws<PlatformNotSupportedException>(() => new NativeMacStatusItem());
    }
}
