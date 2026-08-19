using System.Runtime.InteropServices;
using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.Platform.MacOS.Tests;

public sealed class MacNativeCallbackGuardTests
{
    [Fact]
    public void Exception_does_not_escape_native_boundary()
    {
        Exception? captured = null;
        MacNativeCallbackGuard.Run(
            () => throw new InvalidOperationException("Bearer secret-value"),
            ex => captured = ex);

        Assert.NotNull(captured);
        var description = MacNativeCallbackGuard.Describe(captured);
        Assert.Contains("InvalidOperationException", description, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value", description, StringComparison.Ordinal);
    }

    [Fact]
    public void Exception_is_swallowed_when_no_sink_is_registered()
    {
        var previous = MacNativeCallbackGuard.ErrorSink;
        MacNativeCallbackGuard.ErrorSink = null;
        try
        {
            MacNativeCallbackGuard.Run(() => throw new InvalidOperationException("boom"));
        }
        finally
        {
            MacNativeCallbackGuard.ErrorSink = previous;
        }
    }

    [Fact]
    public void Error_sink_failure_does_not_escape()
    {
        MacNativeCallbackGuard.Run(
            () => throw new InvalidOperationException("panel"),
            _ => throw new InvalidOperationException("logger failed"));
    }
}

public sealed class MacStatusItemGeometryTests
{
    [Fact]
    public void Intel_does_not_query_nsrect_through_objc_msgsend()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return;
        }

        Assert.False(MacStatusItemGeometry.CanQueryButtonFrame);
    }
}
