using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Navigation;

namespace CheckmkDesktopNotifier.Core.Abstractions;

public interface ICheckmkProblemNavigator
{
    /// <summary>
    /// Opens the interactive Checkmk GUI for this host or service in the default browser.
    /// Never mutates incident, Seen, ACK, Take, or downtime state.
    /// </summary>
    CheckmkNavigationResult Open(MonitoredObjectId id);
}
