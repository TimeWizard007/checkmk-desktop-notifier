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
}
