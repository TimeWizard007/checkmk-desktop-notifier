namespace CheckmkDesktopNotifier.Core.Acknowledgements;

public enum TakeRowVisual
{
    Hidden = 0,
    Take = 1,
    Taking = 2,
    Taken = 3,
    Acknowledged = 4,
    Releasing = 5
}

public static class TakeRowPresentation
{
    public static TakeRowVisual Classify(
        bool alreadyAcknowledged,
        bool isTakenByNotifier,
        bool canOfferTake,
        bool isTakingThis,
        bool isReleasingThis = false)
    {
        if (isReleasingThis)
        {
            return TakeRowVisual.Releasing;
        }

        if (alreadyAcknowledged)
        {
            return isTakenByNotifier ? TakeRowVisual.Taken : TakeRowVisual.Acknowledged;
        }

        if (isTakingThis)
        {
            return TakeRowVisual.Taking;
        }

        return canOfferTake ? TakeRowVisual.Take : TakeRowVisual.Hidden;
    }
}
