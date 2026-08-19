namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// macOS menu-bar status item. Tests use a fake; production uses
/// <see cref="NativeMacStatusItem"/>.
/// </summary>
public interface IMacStatusItem : IDisposable
{
    event EventHandler? Activated;

    event EventHandler? OpenProblemsRequested;

    event EventHandler? OpenSettingsRequested;

    event EventHandler? OpenCheckmkRequested;

    event EventHandler? QuitRequested;

    void SetTitle(string title);

    void SetToolTip(string toolTip);

    bool TryGetAnchor(out MacStatusItemAnchor anchor);
}

public readonly record struct MacStatusItemAnchor(double X, double Y, double Width, double Height);
