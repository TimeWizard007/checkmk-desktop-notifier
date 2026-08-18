namespace CheckmkDesktopNotifier.Core.Domain;

/// <summary>
/// Checkmk <c>acknowledgement_type</c>: 1 = normal, 2 = sticky. Other/missing values map to <see cref="None"/>.
/// </summary>
public enum AcknowledgementType
{
    None = 0,
    Normal = 1,
    Sticky = 2
}
