using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public sealed class DelegatingCheckmkClient : ICheckmkClient
{
    private readonly object _gate = new();
    private ICheckmkClient _inner;

    public DelegatingCheckmkClient(ICheckmkClient inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public ICheckmkClient Inner
    {
        get
        {
            lock (_gate)
            {
                return _inner;
            }
        }
    }

    public void SetInner(ICheckmkClient inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        lock (_gate)
        {
            _inner = inner;
        }
    }

    public Task<ProblemSnapshot> GetCurrentProblemsAsync(CancellationToken cancellationToken = default)
    {
        ICheckmkClient inner;
        lock (_gate)
        {
            inner = _inner;
        }

        return inner.GetCurrentProblemsAsync(cancellationToken);
    }
}

public sealed class UnconfiguredCheckmkClient : ICheckmkClient
{
    public Task<ProblemSnapshot> GetCurrentProblemsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ProblemSnapshot.Failure(
            DateTimeOffset.UtcNow,
            SnapshotErrorKind.Configuration,
            "Checkmk is not configured."));
    }
}
