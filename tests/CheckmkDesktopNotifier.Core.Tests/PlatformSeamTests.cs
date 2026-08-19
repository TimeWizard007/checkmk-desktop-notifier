using CheckmkDesktopNotifier.Core;
using CheckmkDesktopNotifier.Core.Storage;
using CheckmkDesktopNotifier.Core.Threading;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class PlatformSeamTests
{
    [Fact]
    public void Immediate_ui_thread_runs_inline()
    {
        var ran = false;
        ImmediateUiThread.Instance.Invoke(() => ran = true);
        Assert.True(ran);

        ran = false;
        ImmediateUiThread.Instance.Post(() => ran = true);
        Assert.True(ran);
        Assert.True(ImmediateUiThread.Instance.CheckAccess());
    }

    [Fact]
    public void Immediate_ui_thread_rejects_null_actions()
    {
        Assert.Throws<ArgumentNullException>(() => ImmediateUiThread.Instance.Invoke(null!));
        Assert.Throws<ArgumentNullException>(() => ImmediateUiThread.Instance.Post(null!));
    }

    [Fact]
    public void Explicit_user_data_directory_returns_supplied_path()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cdn-user-data", Guid.NewGuid().ToString("N"));
        var userData = new ExplicitUserDataDirectory(directory);
        Assert.Equal(directory, userData.GetDirectory());
    }

    [Fact]
    public void Install_layout_is_computed_from_supplied_root_not_os_special_folders()
    {
        var root = Path.Combine(Path.GetTempPath(), "cdn-fake-localappdata");
        var install = InstallLayout.GetPerUserInstallDirectory(root);
        var data = InstallLayout.GetPerUserDataDirectory(root);
        Assert.Equal(Path.Combine(root, "Programs", "CheckmkDesktopNotifier"), install);
        Assert.Equal(Path.Combine(root, "CheckmkDesktopNotifier"), data);
        Assert.True(InstallLayout.BinariesAreSeparateFromUserData(root));
    }

    [Fact]
    public void Windows_credential_store_lives_in_platform_windows()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root,
            "src/CheckmkDesktopNotifier.Platform.Windows/WindowsCredentialSecretStore.cs")));
        Assert.False(File.Exists(Path.Combine(
            root,
            "src/CheckmkDesktopNotifier.Infrastructure/Secrets/WindowsCredentialSecretStore.cs")));
        Assert.True(File.Exists(Path.Combine(
            root,
            "src/CheckmkDesktopNotifier.Platform.Windows/WindowsHkcuRunAutostartStore.cs")));
        Assert.True(File.Exists(Path.Combine(
            root,
            "src/CheckmkDesktopNotifier.Platform.Windows/WindowsShellUriLauncher.cs")));
        Assert.True(File.Exists(Path.Combine(
            root,
            "src/CheckmkDesktopNotifier.Platform.MacOS/MacUserDataDirectory.cs")));
        Assert.True(File.Exists(Path.Combine(
            root,
            "src/CheckmkDesktopNotifier.Platform.MacOS/MacKeychainSecretStore.cs")));
        Assert.True(File.Exists(Path.Combine(
            root,
            "src/CheckmkDesktopNotifier.App.MacOS/CheckmkDesktopNotifier.App.MacOS.csproj")));
    }

    [Fact]
    public void Single_instance_identity_remains_windows_local_namespace()
    {
        Assert.StartsWith(@"Local\", SingleInstanceIdentity.MutexName, StringComparison.Ordinal);
        Assert.StartsWith(@"Local\", SingleInstanceIdentity.ActivateEventName, StringComparison.Ordinal);
        Assert.DoesNotContain(@"Global\", SingleInstanceIdentity.MutexName, StringComparison.Ordinal);
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
