namespace CheckmkDesktopNotifier.Core;

public static class ProductInfo
{
    public const string ProductName = "Checkmk Desktop Notifier";

    public const string Description = "Desktop monitor and notifier for Checkmk";

    public const string Author = "TimeWizard007";

    public const string Copyright = "Copyright © 2026 TimeWizard007";

    public const string Disclaimer =
        "Independent open-source project. Not affiliated with Checkmk GmbH.";

    public const string RepositoryUrl = "https://github.com/TimeWizard007/checkmk-desktop-notifier";

    public static Uri Repository { get; } = new(RepositoryUrl, UriKind.Absolute);
}
