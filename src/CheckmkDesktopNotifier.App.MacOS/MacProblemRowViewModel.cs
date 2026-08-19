using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CheckmkDesktopNotifier.App.MacOS;

public sealed partial class MacProblemRowViewModel : ObservableObject
{
    private readonly Action<MacProblemRowViewModel> _openInCheckmk;
    private readonly Action<MacProblemRowViewModel> _toggleSeen;
    private readonly Action<MacProblemRowViewModel>? _take;
    private readonly Action<MacProblemRowViewModel>? _release;

    public MacProblemRowViewModel(
        OpenIncident incident,
        Action<MacProblemRowViewModel> openInCheckmk,
        Action<MacProblemRowViewModel> toggleSeen,
        TakeRowVisual visual,
        bool canRelease,
        Action<MacProblemRowViewModel>? take = null,
        Action<MacProblemRowViewModel>? release = null)
    {
        Incident = incident ?? throw new ArgumentNullException(nameof(incident));
        _openInCheckmk = openInCheckmk ?? throw new ArgumentNullException(nameof(openInCheckmk));
        _toggleSeen = toggleSeen ?? throw new ArgumentNullException(nameof(toggleSeen));
        TakeVisual = visual;
        CanRelease = canRelease && visual == TakeRowVisual.Taken;
        _take = take;
        _release = release;
    }

    public OpenIncident Incident { get; }

    public MonitoredObjectId ObjectId => Incident.ObjectId;

    public string HostName => Incident.ObjectId.HostName;

    public string HostDisplay => MacTextEllipsis.Fit(HostName, 48);

    public string ServiceName =>
        Incident.ObjectId.Kind == ObjectKind.Host
            ? "Host"
            : Incident.ObjectId.ServiceDescription ?? string.Empty;

    public string ServiceDisplay => MacTextEllipsis.Fit(ServiceName, 56);

    public Severity Severity => Incident.Severity;

    public bool IsCritical => Severity == Severity.Critical;

    public bool IsWarning => Severity == Severity.Warning;

    public bool IsUnknown => Severity == Severity.Unknown;

    public string SeverityText => Incident.Severity switch
    {
        Severity.Critical => "CRIT",
        Severity.Warning => "WARN",
        Severity.Unknown => "UNK",
        _ => Incident.Severity.ToString()
    };

    public string Summary => Incident.LastSummary ?? string.Empty;

    public string SummaryDisplay => MacTextEllipsis.Fit(Summary, 160);

    public bool IsNew => !Incident.IsSeen;

    public bool IsSeen => Incident.IsSeen;

    public string SeenLabel => IsNew ? "NEW" : "Seen";

    public string ToggleSeenLabel => IsNew ? "Mark seen" : "Mark unseen";

    public TakeRowVisual TakeVisual { get; }

    public bool ShowTake => TakeVisual == TakeRowVisual.Take;

    public bool ShowTaking => TakeVisual == TakeRowVisual.Taking;

    public bool ShowTaken => TakeVisual == TakeRowVisual.Taken;

    public bool ShowAck => TakeVisual == TakeRowVisual.Acknowledged;

    public bool ShowReleasing => TakeVisual == TakeRowVisual.Releasing;

    public bool CanTake => ShowTake;

    public bool CanRelease { get; }

    public bool ShowTakenBadge => ShowTaken && !CanRelease;

    public string TakeButtonText => ShowTaking ? MacUiCopy.Taking : MacUiCopy.Take;

    public string ReleaseButtonText => ShowReleasing ? MacUiCopy.Releasing : MacUiCopy.Release;

    public string TakeStateText =>
        ShowTaken || ShowReleasing
            ? "Taken by " + (Incident.TakenByDisplayName ?? "notifier")
            : ShowAck
                ? "ACK"
                : ShowTaking
                    ? MacUiCopy.Taking
                    : string.Empty;

    public string TakenByDisplayName => Incident.TakenByDisplayName ?? string.Empty;

    [RelayCommand]
    private void OpenInCheckmk() => _openInCheckmk(this);

    [RelayCommand]
    private void ToggleSeen() => _toggleSeen(this);

    [RelayCommand]
    private void Take() => _take?.Invoke(this);

    [RelayCommand]
    private void Release() => _release?.Invoke(this);
}
