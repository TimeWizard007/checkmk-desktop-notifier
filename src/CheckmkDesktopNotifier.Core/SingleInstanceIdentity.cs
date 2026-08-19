namespace CheckmkDesktopNotifier.Core;

/// <summary>
/// Windows per-user kernel object names for single-instance. <c>Local\</c> is this
/// Windows logon session, not <c>Global\</c> (no admin). A future macOS host must
/// not reuse these names; it should plug in at the composition root with its own
/// lock (bundle identifier / <c>NSRunningApplication</c>), not this type.
/// </summary>
public static class SingleInstanceIdentity
{
    public const string MutexName = @"Local\TimeWizard007.CheckmkDesktopNotifier";

    public const string ActivateEventName = @"Local\TimeWizard007.CheckmkDesktopNotifier.Activate";
}
