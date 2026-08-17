namespace CheckmkDesktopNotifier.Core;

public static class ProductInfo
{
    public const string ProductName = "Checkmk Desktop Notifier";

    public const string Author = "TimeWizard007";

    public const string RepositoryUrl = "https://github.com/TimeWizard007/checkmk-desktop-notifier";

    public static Uri Repository { get; } = new(RepositoryUrl, UriKind.Absolute);
}
