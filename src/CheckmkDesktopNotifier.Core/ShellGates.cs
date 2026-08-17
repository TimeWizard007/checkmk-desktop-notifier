namespace CheckmkDesktopNotifier.Core;

public sealed class SingleInstanceGate
{
    private int _open;

    public bool IsOpen => Volatile.Read(ref _open) == 1;

    public bool TryEnter() => Interlocked.CompareExchange(ref _open, 1, 0) == 0;

    public void Exit() => Interlocked.Exchange(ref _open, 0);
}

public sealed class ShutdownGate
{
    private int _started;

    public bool HasStarted => Volatile.Read(ref _started) == 1;

    public bool TryBegin() => Interlocked.CompareExchange(ref _started, 1, 0) == 0;
}
