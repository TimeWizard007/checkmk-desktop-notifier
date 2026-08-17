namespace CheckmkDesktopNotifier.Core;

public enum ShellPhase
{
    Initializing = 0,
    Ready = 1,
    ShuttingDown = 2
}

public enum ShellConnectionLabel
{
    Initializing = 0,
    SetupRequired = 1,
    Refreshing = 2,
    Connected = 3,
    Error = 4,
    None = 5
}

public enum SessionPollerKind
{
    Idle = 0,
    Refreshing = 1,
    Connected = 2,
    Error = 3
}

public static class ShellConnectionLabelMapper
{
    public static ShellConnectionLabel Map(
        ShellPhase phase,
        bool unconfiguredReal,
        SessionPollerKind poller)
    {
        if (phase == ShellPhase.Initializing)
        {
            return ShellConnectionLabel.Initializing;
        }

        if (phase == ShellPhase.ShuttingDown)
        {
            return ShellConnectionLabel.None;
        }

        if (unconfiguredReal)
        {
            return ShellConnectionLabel.SetupRequired;
        }

        return poller switch
        {
            SessionPollerKind.Refreshing => ShellConnectionLabel.Refreshing,
            SessionPollerKind.Connected => ShellConnectionLabel.Connected,
            SessionPollerKind.Error => ShellConnectionLabel.Error,
            _ => ShellConnectionLabel.None
        };
    }
}
