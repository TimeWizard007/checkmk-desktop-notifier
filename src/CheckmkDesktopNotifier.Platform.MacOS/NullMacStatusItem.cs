namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// No-op status item for tests and non-macOS. Does not create a menu-bar icon.
/// </summary>
public sealed class NullMacStatusItem : IMacStatusItem
{
    public event EventHandler? Activated;

    public event EventHandler? OpenProblemsRequested;

    public event EventHandler? OpenSettingsRequested;

    public event EventHandler? OpenCheckmkRequested;

    public event EventHandler? QuitRequested;

    public string? Title { get; private set; }

    public string? ToolTip { get; private set; }

    public void SetTitle(string title) => Title = title ?? string.Empty;

    public void SetToolTip(string toolTip) => ToolTip = toolTip ?? string.Empty;

    public bool TryGetAnchor(out MacStatusItemAnchor anchor)
    {
        anchor = default;
        return false;
    }

    public void RaiseActivated() => Activated?.Invoke(this, EventArgs.Empty);

    public void RaiseOpenProblems() => OpenProblemsRequested?.Invoke(this, EventArgs.Empty);

    public void RaiseOpenSettings() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

    public void RaiseOpenCheckmk() => OpenCheckmkRequested?.Invoke(this, EventArgs.Empty);

    public void RaiseQuit() => QuitRequested?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
    }
}
