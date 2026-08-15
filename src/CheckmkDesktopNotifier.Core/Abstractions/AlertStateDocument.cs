using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Abstractions;

public sealed class AlertStateDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public DateTimeOffset? LastSuccessfulPollUtc { get; init; }

    public IReadOnlyList<OpenIncident> Incidents { get; init; } = Array.Empty<OpenIncident>();
}
