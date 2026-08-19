namespace CheckmkDesktopNotifier.App.MacOS;

/// <summary>
/// Creates the problem panel once. Repeated left-click toggles must not allocate
/// additional windows.
/// </summary>
public sealed class MacSingleInstanceToggle<T> where T : class
{
    private T? _instance;

    public int CreateCount { get; private set; }

    public T? Instance => _instance;

    public T GetOrCreate(Func<T> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        if (_instance is not null)
        {
            return _instance;
        }

        var created = create() ?? throw new InvalidOperationException("The window factory returned null.");
        CreateCount++;
        _instance = created;
        return created;
    }
}
