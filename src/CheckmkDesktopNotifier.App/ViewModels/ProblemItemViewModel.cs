using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.App.ViewModels;

public sealed class ProblemItemViewModel
{
    public required MonitoredObjectId ObjectId { get; init; }

    public required string HostName { get; init; }

    public required string ServiceName { get; init; }

    public required string SeverityText { get; init; }

    public required Severity Severity { get; init; }

    public string? PluginOutput { get; init; }

    public required bool IsNew { get; init; }

    public required bool IsSeen { get; init; }

    public required bool IsAcknowledged { get; init; }

    public required bool IsInDowntime { get; init; }

    public TakeRowVisual TakeVisual { get; init; }

    public string AcknowledgementLabel { get; init; } = string.Empty;

    public string TakeButtonText { get; init; } = string.Empty;

    public required string EyeTooltip { get; init; }

    public bool ShowTakeButton => TakeVisual is TakeRowVisual.Take or TakeRowVisual.Taking;

    public bool CanTake => TakeVisual == TakeRowVisual.Take;

    public bool ShowAcknowledgementBadge =>
        TakeVisual is TakeRowVisual.Taken or TakeRowVisual.Acknowledged;
}
