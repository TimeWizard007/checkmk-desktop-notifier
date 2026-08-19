using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.Platform.MacOS.Tests;

public sealed class MacUserDataDirectoryTests
{
    [Fact]
    public void GetDirectory_uses_library_application_support()
    {
        var path = MacUserDataDirectory.GetDirectory("/Users/ops");
        Assert.Equal(
            Path.Combine("/Users/ops", "Library", "Application Support", AppStoragePaths.ApplicationFolderName),
            path);
        Assert.DoesNotContain(".local/share", path, StringComparison.Ordinal);
        Assert.DoesNotContain("AppData", path, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LocalApplicationData", path, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDirectory_rejects_empty_home()
    {
        Assert.Throws<ArgumentException>(() => MacUserDataDirectory.GetDirectory(" "));
    }

    [Fact]
    public void ResolveHomeDirectory_prefers_HOME_over_user_profile()
    {
        var home = MacUserDataDirectory.ResolveHomeDirectory("/Users/from-home", "/Users/from-profile");
        Assert.Equal("/Users/from-home", home);
    }

    [Fact]
    public void ResolveHomeDirectory_uses_user_profile_when_HOME_missing()
    {
        var home = MacUserDataDirectory.ResolveHomeDirectory(null, "/Users/from-profile");
        Assert.Equal("/Users/from-profile", home);
    }

    [Fact]
    public void ResolveHomeDirectory_throws_when_home_cannot_be_determined()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MacUserDataDirectory.ResolveHomeDirectory("  ", ""));
    }

    [Fact]
    public void Instance_returns_constructed_application_support_path()
    {
        var directory = new MacUserDataDirectory("/Users/alice");
        Assert.Equal(
            Path.Combine("/Users/alice", "Library", "Application Support", "CheckmkDesktopNotifier"),
            directory.GetDirectory());
    }

    [Fact]
    public void Source_does_not_use_local_application_data()
    {
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "src/CheckmkDesktopNotifier.Platform.MacOS/MacUserDataDirectory.cs"));
        Assert.DoesNotContain("LocalApplicationData", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SpecialFolder.ApplicationData", source, StringComparison.Ordinal);
        Assert.Contains("Application Support", source, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CheckmkDesktopNotifier.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
