using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Threading;
using CheckmkDesktopNotifier.Infrastructure.Notifications;

namespace CheckmkDesktopNotifier.App.MacOS;

/// <summary>
/// AppKit notification delivery must run on the UI thread. Policy stays in
/// <see cref="INotificationCoordinator"/>.
/// </summary>
public sealed class PostedNotificationService : INotificationService
{
    private readonly IUiThread _uiThread;
    private readonly INotificationService _inner;

    public PostedNotificationService(IUiThread uiThread, INotificationService inner)
    {
        _uiThread = uiThread ?? throw new ArgumentNullException(nameof(uiThread));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public void Show(IncidentAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _uiThread.Post(() => _inner.Show(alert));
    }
}
