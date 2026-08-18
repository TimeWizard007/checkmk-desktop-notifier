using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Notifications;

namespace CheckmkDesktopNotifier.Infrastructure.Notifications;

public interface INotificationService
{
    void Show(IncidentAlert alert);
}

public interface IAlertSoundService
{
    void Play();
}

public interface INotificationCoordinator
{
    /// <summary>
    /// Emit visual/sound alerts for <see cref="AlertDelta.Opened"/> after host-failure grouping.
    /// Child services of an active HARD host DOWN/UNREACHABLE are not notified separately.
    /// Grouping never marks Seen and never changes Core incident identities.
    /// <paramref name="wasVirginLocalState"/> must be captured <em>before</em> <c>ApplySnapshot</c>.
    /// </summary>
    void Process(ProblemSnapshot snapshot, AlertDelta delta, bool wasVirginLocalState);
}

public interface IUserPreferences
{
    bool MuteSound { get; }

    int VolumePercent { get; }

    NotificationSoundSource SoundSource { get; }

    string? CustomSoundFileName { get; }

    bool TakeEnabled { get; }

    string? TakeDisplayName { get; }

    void SetMuteSound(bool mute);

    void SetVolumePercent(int volumePercent);

    void SetSoundSource(NotificationSoundSource source);

    void SetCustomSoundFileName(string? fileName);

    void SetTakeEnabled(bool enabled);

    void SetTakeDisplayName(string? displayName);

    event EventHandler? Changed;
}

public static class MuteCommands
{
    public static void Toggle(IUserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        preferences.SetMuteSound(!preferences.MuteSound);
    }

    public static string MenuHeader(IUserPreferences preferences, string muteLabel, string unmuteLabel)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return preferences.MuteSound ? unmuteLabel : muteLabel;
    }
}

/// <summary>
/// Plays the currently selected notifier sound at the configured volume without creating incidents.
/// Bypasses Mute so the asset can be verified while notifications are muted.
/// </summary>
public static class AlertSoundPreview
{
    public static void Play(IAlertSoundService sound)
    {
        ArgumentNullException.ThrowIfNull(sound);
        try
        {
            sound.Play();
        }
        catch (Exception)
        {
        }
    }
}
