using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Notifications;

namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// macOS notification delivery only. Policy stays in <see cref="INotificationCoordinator"/>.
/// </summary>
public sealed class RecordingMacNotificationService : INotificationService
{
    public IList<IncidentAlert> Shown { get; } = new List<IncidentAlert>();

    public bool ThrowOnShow { get; set; }

    public Action<IncidentAlert>? OnActivate { get; set; }

    public void Show(IncidentAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        if (ThrowOnShow)
        {
            throw new InvalidOperationException("Notification delivery failed.");
        }

        Shown.Add(alert);
    }

    public void ActivateLast()
    {
        if (Shown.Count == 0)
        {
            return;
        }

        OnActivate?.Invoke(Shown[^1]);
    }
}

public sealed class GuardedNotificationService : INotificationService
{
    private readonly INotificationService _inner;

    public GuardedNotificationService(INotificationService inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public int FailureCount { get; private set; }

    public void Show(IncidentAlert alert)
    {
        try
        {
            _inner.Show(alert);
        }
        catch (Exception)
        {
            FailureCount++;
        }
    }
}

/// <summary>
/// No-op delivery when the process is not a bundled macOS app. Policy still
/// runs in <see cref="INotificationCoordinator"/>; only delivery is skipped.
/// </summary>
public sealed class DisabledMacNotificationService : INotificationService
{
    public void Show(IncidentAlert alert) => ArgumentNullException.ThrowIfNull(alert);
}

public static class MacNotificationFactory
{
    public static INotificationService Create(Action<MonitoredObjectId>? onActivate = null)
    {
        try
        {
            var layout = MacAppBundleLayout.Detect(Environment.ProcessPath);
            var liveId = OperatingSystem.IsMacOS() ? NotifyObjC.TryGetMainBundleIdentifier() : null;
            var backend = MacNotificationEnvironment.SelectBackend(
                OperatingSystem.IsMacOS(),
                liveId,
                layout);
            return Create(backend, onActivate);
        }
        catch (Exception)
        {
            return new DisabledMacNotificationService();
        }
    }

    public static INotificationService Create(
        MacNotificationBackend backend,
        Action<MonitoredObjectId>? onActivate = null)
    {
        try
        {
            return backend switch
            {
                MacNotificationBackend.Native when OperatingSystem.IsMacOS() =>
                    new NativeMacNotificationService(
                        onActivate,
                        requestUserNotifications: NotifyObjC.HasMainBundleIdentifier()),
                MacNotificationBackend.Recording => new RecordingMacNotificationService
                {
                    OnActivate = onActivate is null ? null : alert => onActivate(alert.ObjectId)
                },
                _ => new DisabledMacNotificationService()
            };
        }
        catch (Exception)
        {
            return new DisabledMacNotificationService();
        }
    }
}
