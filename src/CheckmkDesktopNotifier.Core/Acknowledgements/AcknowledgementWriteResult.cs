namespace CheckmkDesktopNotifier.Core.Acknowledgements;

public enum AcknowledgementWriteStatus
{
    Success = 0,
    Unauthorized = 1,
    Forbidden = 2,
    InvalidRequest = 3,
    Unavailable = 4,
    Canceled = 5,
    NotConfigured = 6
}

public sealed record AcknowledgementWriteResult(AcknowledgementWriteStatus Status, string UserMessage)
{
    public static AcknowledgementWriteResult Success { get; } =
        new(AcknowledgementWriteStatus.Success, string.Empty);

    public static AcknowledgementWriteResult NotConfigured { get; } =
        new(AcknowledgementWriteStatus.NotConfigured, "Take is not available.");

    public static AcknowledgementWriteResult Unauthorized { get; } =
        new(AcknowledgementWriteStatus.Unauthorized, "Checkmk authentication failed.");

    public static AcknowledgementWriteResult Forbidden { get; } =
        new(AcknowledgementWriteStatus.Forbidden, "This Checkmk account cannot acknowledge problems.");

    public static AcknowledgementWriteResult InvalidRequest { get; } =
        new(AcknowledgementWriteStatus.InvalidRequest, "Could not acknowledge the problem.");

    public static AcknowledgementWriteResult Unavailable { get; } =
        new(AcknowledgementWriteStatus.Unavailable, "Could not acknowledge the problem.");

    public static AcknowledgementWriteResult Canceled { get; } =
        new(AcknowledgementWriteStatus.Canceled, "The acknowledgement request was canceled.");
}
