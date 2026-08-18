namespace CheckmkDesktopNotifier.Core.Acknowledgements;

public static class TakeEligibility
{
    public static bool CanOfferTake(
        bool takeEnabled,
        string? displayName,
        bool isRealMonitoring,
        bool acknowledgeForbidden,
        bool alreadyAcknowledged,
        bool isTaking,
        bool isReady)
    {
        if (!isReady
            || !isRealMonitoring
            || acknowledgeForbidden
            || alreadyAcknowledged
            || isTaking
            || !takeEnabled)
        {
            return false;
        }

        return TakeDisplayName.Normalize(displayName) is not null;
    }

    /// <summary>
    /// Release is team coordination, not an ACL. Any admin may release any CDN Take.
    /// Generic/manual Checkmk ACK is never eligible.
    /// </summary>
    public static bool CanOfferRelease(
        bool isAcknowledged,
        bool isTakenByNotifier,
        bool isRealMonitoring,
        bool acknowledgeForbidden,
        bool isBusy,
        bool isReady) =>
        isReady
        && isRealMonitoring
        && !acknowledgeForbidden
        && !isBusy
        && IsCdnTake(isAcknowledged, isTakenByNotifier);

    public static bool IsCdnTake(bool isAcknowledged, bool isTakenByNotifier) =>
        isAcknowledged && isTakenByNotifier;
}
