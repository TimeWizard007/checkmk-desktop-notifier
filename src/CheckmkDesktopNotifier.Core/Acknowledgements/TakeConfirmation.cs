namespace CheckmkDesktopNotifier.Core.Acknowledgements;

/// <summary>
/// Take proceeds only after an explicit affirmative dialog result.
/// Close, Escape, Cancel, and a missing dialog do not write Checkmk.
/// </summary>
public static class TakeConfirmation
{
    public static bool ShouldProceed(bool? dialogResult) => dialogResult == true;
}
