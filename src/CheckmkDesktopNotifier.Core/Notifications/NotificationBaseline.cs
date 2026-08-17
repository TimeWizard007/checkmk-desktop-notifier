namespace CheckmkDesktopNotifier.Core.Notifications;

/// <summary>
/// Startup-safety rule for notifications. Does not change incident lifecycle.
/// </summary>
public static class NotificationBaseline
{
    /// <summary>
    /// True when the local store has never completed a successful poll and currently has no open incidents.
    /// The first successful snapshot against a populated Checkmk would otherwise open every current HARD problem.
    /// Those incidents still appear in the UI; they must not all emit notifications.
    /// </summary>
    public static bool IsVirginLocalState(int openIncidentCount, DateTimeOffset? lastSuccessfulPollUtc) =>
        openIncidentCount == 0 && lastSuccessfulPollUtc is null;
}
