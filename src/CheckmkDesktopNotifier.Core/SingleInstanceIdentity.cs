namespace CheckmkDesktopNotifier.Core;

/// <summary>
/// Per-user session names. <c>Local\</c> is this Windows logon; not <c>Global\</c> (no admin).
/// </summary>
public static class SingleInstanceIdentity
{
    public const string MutexName = @"Local\TimeWizard007.CheckmkDesktopNotifier";

    public const string ActivateEventName = @"Local\TimeWizard007.CheckmkDesktopNotifier.Activate";
}
