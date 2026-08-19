using CheckmkDesktopNotifier.Core.Storage;
using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// macOS per-user data: <c>~/Library/Application Support/CheckmkDesktopNotifier</c>.
/// Does not use <c>~/.local/share</c> or Windows LocalAppData.
/// </summary>
public sealed class MacUserDataDirectory : IUserDataDirectory
{
    public const string LibraryFolderName = "Library";
    public const string ApplicationSupportFolderName = "Application Support";

    private readonly string _directory;

    public MacUserDataDirectory()
        : this(ResolveHomeDirectory(
            Environment.GetEnvironmentVariable("HOME"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
    {
    }

    public MacUserDataDirectory(string homeDirectory)
    {
        _directory = GetDirectory(homeDirectory);
    }

    public string GetDirectory() => _directory;

    public static string GetDirectory(string homeDirectory)
    {
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            throw new ArgumentException("Home directory must not be empty.", nameof(homeDirectory));
        }

        return Path.Combine(
            homeDirectory.Trim(),
            LibraryFolderName,
            ApplicationSupportFolderName,
            AppStoragePaths.ApplicationFolderName);
    }

    public static string ResolveHomeDirectory(string? homeEnvironment, string? userProfileFolder)
    {
        if (!string.IsNullOrWhiteSpace(homeEnvironment))
        {
            return homeEnvironment.Trim();
        }

        if (!string.IsNullOrWhiteSpace(userProfileFolder))
        {
            return userProfileFolder.Trim();
        }

        throw new InvalidOperationException("The user home directory could not be determined.");
    }
}
