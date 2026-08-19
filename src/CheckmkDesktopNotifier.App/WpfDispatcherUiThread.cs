using System.Windows;
using System.Windows.Threading;
using CheckmkDesktopNotifier.Core.Threading;

namespace CheckmkDesktopNotifier.App;

/// <summary>
/// WPF Dispatcher marshaling. Matches v1.2.0 ShellViewModel behavior:
/// missing <see cref="Application.Current"/> dispatcher runs inline.
/// </summary>
public sealed class WpfDispatcherUiThread : IUiThread
{
    private readonly Func<Dispatcher?> _dispatcher;

    public WpfDispatcherUiThread()
        : this(() => Application.Current?.Dispatcher)
    {
    }

    public WpfDispatcherUiThread(Func<Dispatcher?> dispatcher)
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

        dispatcher.BeginInvoke(action);
    }
}
