namespace CheckmkDesktopNotifier.App.MacOS;

/// <summary>
/// Actions the native status item may request. Invoked on the Avalonia UI thread.
/// </summary>
public sealed class MacStatusItemCommands
{
    public required Action ToggleProblems { get; init; }

    public required Action ShowProblems { get; init; }

    public required Action ShowSettings { get; init; }

    public required Action OpenCheckmk { get; init; }

    public required Action Quit { get; init; }
}
