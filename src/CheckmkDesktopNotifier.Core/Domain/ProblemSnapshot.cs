namespace CheckmkDesktopNotifier.Core.Domain;

public sealed class ProblemSnapshot
{
    public bool IsSuccess { get; }
    public DateTimeOffset RetrievedAt { get; }
    public SiteId? SiteId { get; }
    public IReadOnlyList<MonitoredProblem> Problems { get; }
    public SnapshotErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }

    private ProblemSnapshot(
        bool isSuccess,
        DateTimeOffset retrievedAt,
        SiteId? siteId,
        IReadOnlyList<MonitoredProblem> problems,
        SnapshotErrorKind? errorKind,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        RetrievedAt = retrievedAt;
        SiteId = siteId;
        Problems = problems;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public static ProblemSnapshot Success(
        DateTimeOffset retrievedAt,
        SiteId siteId,
        IReadOnlyList<MonitoredProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        return new ProblemSnapshot(
            isSuccess: true,
            retrievedAt,
            siteId,
            problems,
            errorKind: null,
            errorMessage: null);
    }

    public static ProblemSnapshot Failure(
        DateTimeOffset retrievedAt,
        SnapshotErrorKind errorKind,
        string? errorMessage = null,
        SiteId? siteId = null)
    {
        return new ProblemSnapshot(
            isSuccess: false,
            retrievedAt,
            siteId,
            Array.Empty<MonitoredProblem>(),
            errorKind,
            errorMessage);
    }
}
