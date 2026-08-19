using System.Threading;
using CheckmkDesktopNotifier.Core;

namespace CheckmkDesktopNotifier.App;

/// <summary>
/// Per-user single instance on Windows. Second launch signals the existing process
/// via <see cref="SingleInstanceIdentity"/> (<c>Local\</c> mutex + event), then exits.
/// Future macOS must not use this class; the macOS host should own an equivalent
/// composition-root guard.
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private readonly CancellationTokenSource _cts = new();
    private bool _ownsMutex;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
        _ownsMutex = true;
    }

    public static bool TryOwn(out SingleInstanceGuard? guard)
    {
        guard = null;
        var mutex = new Mutex(initiallyOwned: false, SingleInstanceIdentity.MutexName);
        var owned = false;
        try
        {
            owned = mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            owned = true;
        }

        if (!owned)
        {
            mutex.Dispose();
            TrySignalExistingInstance();
            return false;
        }

        var activate = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            SingleInstanceIdentity.ActivateEventName);
        guard = new SingleInstanceGuard(mutex, activate);
        return true;
    }

    public void Listen(Action onActivate)
    {
        ArgumentNullException.ThrowIfNull(onActivate);
        var thread = new Thread(() =>
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (_activate.WaitOne(TimeSpan.FromMilliseconds(400)) && !_cts.IsCancellationRequested)
                    {
                        onActivate();
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
        })
        {
            IsBackground = true,
            Name = "CheckmkDesktopNotifier.Activate"
        };
        thread.Start();
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _activate.Set();
        }
        catch (ObjectDisposedException)
        {
        }

        _activate.Dispose();
        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _ownsMutex = false;
        }

        _mutex.Dispose();
        _cts.Dispose();
    }

    private static void TrySignalExistingInstance()
    {
        try
        {
            using var signal = EventWaitHandle.OpenExisting(SingleInstanceIdentity.ActivateEventName);
            signal.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
