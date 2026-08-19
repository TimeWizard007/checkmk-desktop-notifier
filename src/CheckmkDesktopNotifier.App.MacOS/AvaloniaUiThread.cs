using Avalonia;
using Avalonia.Threading;
using CheckmkDesktopNotifier.Core.Threading;

namespace CheckmkDesktopNotifier.App.MacOS;

/// <summary>
/// Avalonia dispatcher marshaling. Not the WPF <c>WpfDispatcherUiThread</c>.
/// </summary>
public sealed class AvaloniaUiThread : IUiThread
{
    private readonly Func<Dispatcher?> _dispatcher;

    public AvaloniaUiThread()
        : this(() => Dispatcher.UIThread)
    {
    }

    public AvaloniaUiThread(Func<Dispatcher?> dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public bool CheckAccess()
    {
        var dispatcher = _dispatcher();
        return dispatcher is null || dispatcher.CheckAccess();
    }

    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = _dispatcher();
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = _dispatcher();
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Post(action);
    }
}
