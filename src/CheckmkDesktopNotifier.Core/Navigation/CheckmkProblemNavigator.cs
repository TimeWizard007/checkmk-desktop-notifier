using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Navigation;

public sealed class CheckmkProblemNavigator : ICheckmkProblemNavigator
{
    private readonly Func<(string? BaseUrl, string? Site)?> _origin;
    private readonly Action<Uri> _launch;

    public CheckmkProblemNavigator(
        Func<(string? BaseUrl, string? Site)?> origin,
        Action<Uri> launch)
    {
        _origin = origin ?? throw new ArgumentNullException(nameof(origin));
        _launch = launch ?? throw new ArgumentNullException(nameof(launch));
    }

    public CheckmkNavigationResult Open(MonitoredObjectId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        try
        {
            var origin = _origin();
            if (origin is null
                || !CheckmkGuiUriBuilder.TryCreate(origin.Value.BaseUrl, origin.Value.Site, id, out var uri)
                || uri is null)
            {
                return CheckmkNavigationResult.Unavailable;
            }

            _launch(uri);
            return CheckmkNavigationResult.Succeeded(uri);
        }
        catch (Exception)
        {
            return CheckmkNavigationResult.Unavailable;
        }
    }
}
