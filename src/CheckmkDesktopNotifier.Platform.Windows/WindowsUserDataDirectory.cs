using CheckmkDesktopNotifier.Core.Storage;
using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Platform.Windows;

/// <summary>
/// Windows per-user data: <c>%LocalAppData%\CheckmkDesktopNotifier</c>.
/// </summary>
public sealed class WindowsUserDataDirectory : IUserDataDirectory
{
    private readonly string _directory;

    public WindowsUserDataDirectory()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    public WindowsUserDataDirectory(string localApplicationData)
    {
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new ArgumentException("Local application data root must not be empty.", nameof(localApplicationData));
        }

        _directory = Path.Combine(localApplicationData, AppStoragePaths.ApplicationFolderName);
    }

    public string GetDirectory() => _directory;
}
