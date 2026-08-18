namespace CheckmkDesktopNotifier.Core.Acknowledgements;

public enum TakeOperationStatus
{
    Cancelled = 0,
    FeatureDisabled = 1,
    AlreadyAcknowledged = 2,
    Unauthorized = 3,
    Forbidden = 4,
    InvalidRequest = 5,
    Unavailable = 6,
    SentAwaitingRefresh = 7,
    Confirmed = 8
}

public sealed record TakeOperationResult(TakeOperationStatus Status, string? UserMessage = null)
{
    public static TakeOperationResult Cancelled { get; } = new(TakeOperationStatus.Cancelled);

    public static TakeOperationResult FeatureDisabled { get; } =
        new(TakeOperationStatus.FeatureDisabled);

    public static TakeOperationResult AlreadyAcknowledged { get; } =
        new(TakeOperationStatus.AlreadyAcknowledged);

    public static TakeOperationResult Forbidden { get; } =
        new(TakeOperationStatus.Forbidden, "This Checkmk account cannot acknowledge problems.");

    public static TakeOperationResult Unauthorized { get; } =
        new(TakeOperationStatus.Unauthorized, "Checkmk authentication failed.");

    public static TakeOperationResult InvalidRequest { get; } =
        new(TakeOperationStatus.InvalidRequest, "Could not acknowledge the problem.");

    public static TakeOperationResult Unavailable { get; } =
        new(TakeOperationStatus.Unavailable, "Could not acknowledge the problem.");

    public static TakeOperationResult SentAwaitingRefresh { get; } =
        new(TakeOperationStatus.SentAwaitingRefresh, "Acknowledgement sent; waiting for Checkmk refresh.");

    public static TakeOperationResult Confirmed { get; } = new(TakeOperationStatus.Confirmed);
}

public static class TakeWorkflow
{
    public static TakeOperationStatus AfterWrite(
        AcknowledgementWriteStatus writeStatus,
        bool refreshSucceeded,
        bool snapshotAcknowledged)
    {
        return writeStatus switch
        {
            AcknowledgementWriteStatus.Success when refreshSucceeded && snapshotAcknowledged =>
                TakeOperationStatus.Confirmed,
            AcknowledgementWriteStatus.Success => TakeOperationStatus.SentAwaitingRefresh,
            AcknowledgementWriteStatus.Unauthorized => TakeOperationStatus.Unauthorized,
            AcknowledgementWriteStatus.Forbidden => TakeOperationStatus.Forbidden,
            AcknowledgementWriteStatus.InvalidRequest => TakeOperationStatus.InvalidRequest,
            AcknowledgementWriteStatus.Canceled => TakeOperationStatus.Unavailable,
            AcknowledgementWriteStatus.NotConfigured => TakeOperationStatus.FeatureDisabled,
            _ => TakeOperationStatus.Unavailable
        };
    }
}
