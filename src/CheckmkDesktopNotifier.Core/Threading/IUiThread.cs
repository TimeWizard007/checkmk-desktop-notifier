namespace CheckmkDesktopNotifier.Core.Threading;

/// <summary>
/// Marshals work onto the desktop UI thread. Windows uses the WPF Dispatcher;
/// a future macOS host supplies its own implementation.
/// </summary>
public interface IUiThread
{
    bool CheckAccess();

    void Invoke(Action action);

    void Post(Action action);
}

/// <summary>
/// Runs work on the calling thread. Used in tests and when no dispatcher exists.
/// </summary>
public sealed class ImmediateUiThread : IUiThread
{
    public static ImmediateUiThread Instance { get; } = new();

    public bool CheckAccess() => true;

    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
