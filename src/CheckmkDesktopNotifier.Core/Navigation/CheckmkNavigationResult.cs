namespace CheckmkDesktopNotifier.Core.Navigation;

public sealed record CheckmkNavigationResult(bool Opened, Uri? Target)
{
    public static CheckmkNavigationResult Unavailable { get; } = new(false, null);

    public static CheckmkNavigationResult Succeeded(Uri target) => new(true, target);
}
