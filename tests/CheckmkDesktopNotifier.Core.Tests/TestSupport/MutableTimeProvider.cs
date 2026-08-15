namespace CheckmkDesktopNotifier.Core.Tests.TestSupport;

internal sealed class MutableTimeProvider : TimeProvider
{
    public MutableTimeProvider(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; set; }

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
