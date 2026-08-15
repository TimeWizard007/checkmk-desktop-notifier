namespace CheckmkDesktopNotifier.Core.Domain;

public sealed class AlertDelta
{
    public static AlertDelta Empty { get; } = new(
        Array.Empty<OpenIncident>(),
        Array.Empty<RecoveredIncident>(),
        Array.Empty<OpenIncident>());

    public IReadOnlyList<OpenIncident> Opened { get; }
    public IReadOnlyList<RecoveredIncident> Recovered { get; }
    public IReadOnlyList<OpenIncident> SeverityChanged { get; }

    public AlertDelta(
        IReadOnlyList<OpenIncident> opened,
        IReadOnlyList<RecoveredIncident> recovered,
        IReadOnlyList<OpenIncident> severityChanged)
    {
        Opened = opened ?? throw new ArgumentNullException(nameof(opened));
        Recovered = recovered ?? throw new ArgumentNullException(nameof(recovered));
        SeverityChanged = severityChanged ?? throw new ArgumentNullException(nameof(severityChanged));
    }

    public bool IsEmpty => Opened.Count == 0 && Recovered.Count == 0 && SeverityChanged.Count == 0;
}
