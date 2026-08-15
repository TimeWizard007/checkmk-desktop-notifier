using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Mock;

/// <summary>
/// In-memory Checkmk client for tests and later design-time use. Never performs HTTP.
/// </summary>
public sealed class MockCheckmkClient : ICheckmkClient
{
    public ProblemSnapshot NextSnapshot { get; set; } = ProblemSnapshot.Failure(
        DateTimeOffset.UnixEpoch,
        SnapshotErrorKind.Unavailable,
        "No snapshot configured.");

    public Task<ProblemSnapshot> GetCurrentProblemsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(NextSnapshot);
    }
}
