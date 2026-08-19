namespace CheckmkDesktopNotifier.Core.Storage;

/// <summary>
/// Per-user application data directory. Windows uses LocalAppData; macOS will use
/// <c>~/Library/Application Support/CheckmkDesktopNotifier</c>.
/// </summary>
public interface IUserDataDirectory
{
    string GetDirectory();
}

/// <summary>
/// Explicit directory, independent of OS special folders. Used by tests and hosts
/// that already resolved a platform path.
/// </summary>
public sealed class ExplicitUserDataDirectory : IUserDataDirectory
{
    private readonly string _directory;

    public ExplicitUserDataDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("User data directory must not be empty.", nameof(directory));
        }

        _directory = directory;
    }

    public string GetDirectory() => _directory;
}
