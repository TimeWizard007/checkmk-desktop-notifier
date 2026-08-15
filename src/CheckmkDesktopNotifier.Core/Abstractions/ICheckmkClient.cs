using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Abstractions;

/// <summary>
/// Read-only Checkmk adapter. Implementations must not acknowledge or modify problems in Checkmk.
/// </summary>
public interface ICheckmkClient
{
    Task<ProblemSnapshot> GetCurrentProblemsAsync(CancellationToken cancellationToken = default);
}
