using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CheckmkDesktopNotifier.App.MacOS;

public sealed partial class MacProblemRowViewModel : ObservableObject
{
    private readonly Action<MacProblemRowViewModel> _openInCheckmk;
    private readonly Action<MacProblemRowViewModel> _toggleSeen;

    public MacProblemRowViewModel(
        OpenIncident incident,
        Action<MacProblemRowViewModel> openInCheckmk,
        Action<MacProblemRowViewModel> toggleSeen)
    {
        Incident = incident ?? throw new ArgumentNullException(nameof(incident));
        _openInCheckmk = openInCheckmk ?? throw new ArgumentNullException(nameof(openInCheckmk));
        _toggleSeen = toggleSeen ?? throw new ArgumentNullException(nameof(toggleSeen));
        TakeVisual = TakeRowPresentation.Classify(
            incident.IsAcknowledgedInCheckmk,
            incident.IsTakenByNotifier,
            canOfferTake: false,
            isTakingThis: false);
    }

    public OpenIncident Incident { get; }

    public MonitoredObjectId ObjectId => Incident.ObjectId;

    public string HostName => Incident.ObjectId.HostName;

    public string ServiceName =>
        Incident.ObjectId.Kind == ObjectKind.Host
            ? "Host"
            : Incident.ObjectId.ServiceDescription ?? string.Empty;

    public Severity Severity => Incident.Severity;

    public string SeverityText => Incident.Severity switch
    {
        Severity.Critical => "CRIT",
        Severity.Warning => "WARN",
        Severity.Unknown => "UNK",
        _ => Incident.Severity.ToString()
    };

    public string Summary => Incident.LastSummary ?? string.Empty;

    public bool IsNew => !Incident.IsSeen;

    public bool IsSeen => Incident.IsSeen;

    public string SeenLabel => IsNew ? "NEW" : "Seen";

    public string ToggleSeenLabel => IsNew ? "Mark seen" : "Mark unseen";

    public TakeRowVisual TakeVisual { get; }

    public bool ShowTaken => TakeVisual == TakeRowVisual.Taken;

    public bool ShowAck => TakeVisual == TakeRowVisual.Acknowledged;

    public string TakeStateText =>
        ShowTaken
            ? "Taken by " + (Incident.TakenByDisplayName ?? "notifier")
            : ShowAck
                ? "ACK"
                : string.Empty;

    [RelayCommand]
    private void OpenInCheckmk() => _openInCheckmk(this);

    [RelayCommand]
    private void ToggleSeen() => _toggleSeen(this);
}
