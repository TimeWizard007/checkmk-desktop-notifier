namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// Per-user single instance without Windows <c>Local\</c> mutex names.
/// Exclusive lock file plus an activate ping file the owner polls.
/// </summary>
public sealed class MacSingleInstanceLock : IDisposable
{
    public const string LockFileName = "instance.lock";
    public const string ActivateFileName = "instance.activate";

    private readonly string _lockPath;
    private readonly string _activatePath;
    private readonly FileStream _lockStream;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    private MacSingleInstanceLock(string lockPath, string activatePath, FileStream lockStream)
    {
        _lockPath = lockPath;
        _activatePath = activatePath;
        _lockStream = lockStream;
    }

    public string LockPath => _lockPath;

    public static bool TryOwn(string appDataDirectory, out MacSingleInstanceLock? owned)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataDirectory);
        owned = null;
        Directory.CreateDirectory(appDataDirectory);
        var lockPath = Path.Combine(appDataDirectory, LockFileName);
        var activatePath = Path.Combine(appDataDirectory, ActivateFileName);
        try
        {
            var stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            try
            {
                File.Delete(activatePath);
            }
            catch (Exception)
            {
            }

            owned = new MacSingleInstanceLock(lockPath, activatePath, stream);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void SignalExisting(string appDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataDirectory);
        try
        {
            Directory.CreateDirectory(appDataDirectory);
            File.WriteAllText(
                Path.Combine(appDataDirectory, ActivateFileName),
                DateTimeOffset.UtcNow.ToString("o"));
        }
        catch (Exception)
        {
        }
    }

    public void Listen(Action onActivate)
    {
        ArgumentNullException.ThrowIfNull(onActivate);
        var thread = new Thread(() =>
        {
            DateTime lastWrite = DateTime.MinValue;

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (File.Exists(_activatePath))
                    {
                        var write = File.GetLastWriteTimeUtc(_activatePath);
                        if (write > lastWrite)
                        {
                            lastWrite = write;
                            onActivate();
                        }
                    }
                }
                catch (Exception)
                {
                }

                try
                {
                    _cts.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(400));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        })
        {
            IsBackground = true,
            Name = "CheckmkDesktopNotifier.MacActivate"
        };
        thread.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _lockStream.Dispose();
        _cts.Dispose();
        try
        {
            File.Delete(_lockPath);
        }
        catch (Exception)
        {
        }
    }
}
