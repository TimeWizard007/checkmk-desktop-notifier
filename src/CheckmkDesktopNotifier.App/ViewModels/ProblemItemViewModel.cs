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
}
