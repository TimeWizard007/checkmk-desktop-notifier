using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Notifications;

namespace CheckmkDesktopNotifier.Infrastructure.Notifications;

/// <summary>
/// Maps Core <see cref="AlertDelta"/> to desktop alerts. Does not own incident lifecycle.
/// </summary>
public sealed class NotificationCoordinator : INotificationCoordinator
{
    private readonly INotificationService _notifications;
    private readonly IAlertSoundService _sound;
    private readonly IUserPreferences _preferences;

    public NotificationCoordinator(
        INotificationService notifications,
        IAlertSoundService sound,
        IUserPreferences preferences)
    {
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _sound = sound ?? throw new ArgumentNullException(nameof(sound));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    public void Process(ProblemSnapshot snapshot, AlertDelta delta, bool wasVirginLocalState)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(delta);

        if (!snapshot.IsSuccess)
        {
            return;
        }

        if (wasVirginLocalState)
        {
            return;
        }

        foreach (var opened in delta.Opened)
        {
            if (opened.IsSeen)
            {
                continue;
            }

            IncidentAlert alert;
            try
            {
                alert = IncidentAlertFormatter.From(opened);
            }
            catch (Exception)
            {
                continue;
            }

            try
            {
                _notifications.Show(alert);
            }
            catch (Exception)
            {
            }

            if (_preferences.MuteSound)
            {
                continue;
            }

            try
            {
                _sound.Play();
            }
            catch (Exception)
            {
            }
        }
    }
}
