using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.App.MacOS;

public static class MacUiCopy
{
    public const string TakeTitle = "Take this problem?";

    public const string TakeBody =
        "This will acknowledge the problem in Checkmk as a sticky acknowledgement.\n"
        + "Other administrators will see that it is being handled.\n"
        + "Checkmk will stop sending further notifications for this current problem until it returns to OK/UP.";

    public const string Take = "Take";

    public const string Taking = "Taking...";

    public const string Release = "Release";

    public const string Releasing = "Releasing...";

    public const string ReleaseTitle = "Release this problem?";

    public const string ReleaseBodyFormat =
        "This problem is currently taken by {0}.\n"
        + "Releasing it will remove the Checkmk acknowledgement.\n"
        + "The problem will remain active and may start generating notifications again.";

    public const string TakeCouldNot = "Could not acknowledge the problem.";

    public const string ReleaseCouldNot = "Could not release the problem.";

    public const string TakeForbidden = "This Checkmk account cannot acknowledge problems.";

    public const string Cancel = "Cancel";

    public const string Ok = "OK";

    public const string TeamHint =
        "Take is shared through Checkmk acknowledgement. Seen remains local to the current macOS user.";

    public static readonly string LoginItemHint = MacLoginItemCapability.Limitation;
}

public static class MacTextEllipsis
{
    public static string Fit(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength <= 0)
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return maxLength == 1 ? value[..1] : value[..(maxLength - 1)] + "…";
    }
}
