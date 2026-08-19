using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.App.MacOS;

/// <summary>
/// Forwards NSStatusItem events onto the Avalonia UI dispatcher. The native IMP
/// must return before any Avalonia window is shown or activated.
/// </summary>
public sealed class MacStatusItemEventRouter : IDisposable
{
    private readonly IMacStatusItem _statusItem;
    private readonly Action<Action> _marshal;
    private readonly MacStatusItemCommands _commands;
    private readonly Action<Exception>? _onError;
    private bool _disposed;

    public MacStatusItemEventRouter(
        IMacStatusItem statusItem,
        Action<Action> marshal,
        MacStatusItemCommands commands,
        Action<Exception>? onError = null)
    {
        _statusItem = statusItem ?? throw new ArgumentNullException(nameof(statusItem));
        _marshal = marshal ?? throw new ArgumentNullException(nameof(marshal));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _onError = onError;
        _statusItem.Activated += OnActivated;
        _statusItem.OpenProblemsRequested += OnOpenProblems;
        _statusItem.OpenSettingsRequested += OnOpenSettings;
        _statusItem.OpenCheckmkRequested += OnOpenCheckmk;
        _statusItem.QuitRequested += OnQuit;
    }

    public int PostedCount { get; private set; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _statusItem.Activated -= OnActivated;
        _statusItem.OpenProblemsRequested -= OnOpenProblems;
        _statusItem.OpenSettingsRequested -= OnOpenSettings;
        _statusItem.OpenCheckmkRequested -= OnOpenCheckmk;
        _statusItem.QuitRequested -= OnQuit;
    }

    private void OnActivated(object? sender, EventArgs e) => Post(_commands.ToggleProblems);

    private void OnOpenProblems(object? sender, EventArgs e) => Post(_commands.ShowProblems);

    private void OnOpenSettings(object? sender, EventArgs e) => Post(_commands.ShowSettings);

    private void OnOpenCheckmk(object? sender, EventArgs e) => Post(_commands.OpenCheckmk);

    private void OnQuit(object? sender, EventArgs e) => Post(_commands.Quit);

    private void Post(Action action)
    {
        PostedCount++;
        try
        {
            _marshal(() => Run(action));
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    private void Report(Exception exception) => MacNativeCallbackGuard.Report(exception, _onError);
}
