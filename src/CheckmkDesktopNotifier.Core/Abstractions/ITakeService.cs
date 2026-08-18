using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Abstractions;

public interface ITakeService
{
    Task<TakeOperationResult> TakeAsync(MonitoredObjectId id, CancellationToken cancellationToken = default);
}
