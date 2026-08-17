namespace CheckmkDesktopNotifier.Core;

public static class ShutdownSteps
{
    public static readonly string[] Ordered =
    [
        "PreventNewPolling",
        "CancelMonitoringSession",
        "CloseDialogs",
        "CloseProblemList",
        "DisposeTray",
        "ShutdownApplication"
    ];
}
