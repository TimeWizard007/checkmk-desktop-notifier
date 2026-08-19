using System.Xml.Linq;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class MacHostIsolationTests
{
    [Fact]
    public void Windows_app_references_platform_windows_only()
    {
        var app = ReadProject("src/CheckmkDesktopNotifier.App/CheckmkDesktopNotifier.App.csproj");
        Assert.Contains("CheckmkDesktopNotifier.Platform.Windows", app, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckmkDesktopNotifier.Platform.MacOS", app, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckmkDesktopNotifier.App.MacOS", app, StringComparison.Ordinal);
        Assert.Contains("<UseWPF>true</UseWPF>", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Mac_app_does_not_reference_windows_platform_or_ui()
    {
        var app = ReadProject("src/CheckmkDesktopNotifier.App.MacOS/CheckmkDesktopNotifier.App.MacOS.csproj");
        Assert.Contains("CheckmkDesktopNotifier.Platform.MacOS", app, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckmkDesktopNotifier.Platform.Windows", app, StringComparison.Ordinal);
        Assert.DoesNotContain("UseWPF", app, StringComparison.Ordinal);
        Assert.DoesNotContain("UseWindowsForms", app, StringComparison.Ordinal);
        Assert.DoesNotContain("EnableWindowsTargeting", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Win32.Registry", app, StringComparison.Ordinal);
        Assert.DoesNotContain("net8.0-windows", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Mac_platform_does_not_reference_windows_platform()
    {
        var project = ReadProject("src/CheckmkDesktopNotifier.Platform.MacOS/CheckmkDesktopNotifier.Platform.MacOS.csproj");
        Assert.DoesNotContain("CheckmkDesktopNotifier.Platform.Windows", project, StringComparison.Ordinal);
        Assert.DoesNotContain("UseWPF", project, StringComparison.Ordinal);
        Assert.DoesNotContain("net8.0-windows", project, StringComparison.Ordinal);
        Assert.Contains("CheckmkDesktopNotifier.Core", project, StringComparison.Ordinal);
        Assert.Contains("CheckmkDesktopNotifier.Infrastructure", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Mac_sources_do_not_load_windows_secrets_or_installer()
    {
        var root = FindRepoRoot();
        var files = Directory.GetFiles(
                Path.Combine(root, "src/CheckmkDesktopNotifier.Platform.MacOS"),
                "*.cs",
                SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(
                Path.Combine(root, "src/CheckmkDesktopNotifier.App.MacOS"),
                "*.cs",
                SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(
                Path.Combine(root, "src/CheckmkDesktopNotifier.App.MacOS"),
                "*.csproj",
                SearchOption.TopDirectoryOnly));

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("using System.Windows", text, StringComparison.Ordinal);
            Assert.DoesNotContain("DllImport(\"advapi32", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Microsoft.Win32.Registry", text, StringComparison.Ordinal);
            Assert.DoesNotContain("using System.Windows.Forms", text, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Media.SoundPlayer", text, StringComparison.Ordinal);
            Assert.DoesNotContain("NotifyIcon", text, StringComparison.Ordinal);
            Assert.DoesNotContain("CheckmkDesktopNotifier.iss", text, StringComparison.Ordinal);
            Assert.DoesNotContain("new WpfDispatcherUiThread", text, StringComparison.Ordinal);
            Assert.DoesNotContain("new WindowsCredentialSecretStore", text, StringComparison.Ordinal);
            Assert.DoesNotContain("new InMemorySecretStore", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Mac_composition_root_registers_macos_implementations()
    {
        var text = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src/CheckmkDesktopNotifier.App.MacOS/MacDesktopHost.cs"));
        Assert.Contains("new MacUserDataDirectory()", text, StringComparison.Ordinal);
        Assert.Contains("new MacKeychainSecretStore()", text, StringComparison.Ordinal);
        Assert.Contains("IUriLauncher, MacOpenUriLauncher", text, StringComparison.Ordinal);
        Assert.Contains("IUiThread, AvaloniaUiThread", text, StringComparison.Ordinal);
        Assert.Contains("GuiConfigurationService", text, StringComparison.Ordinal);
        Assert.Contains("CheckmkConnectionTester", text, StringComparison.Ordinal);
        Assert.Contains("IMonitoringCoordinator, MonitoringCoordinator", text, StringComparison.Ordinal);
        Assert.Contains("AddCheckmkPolling", text, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsUserDataDirectory", text, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsCredentialSecretStore", text, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsShellUriLauncher", text, StringComparison.Ordinal);
        Assert.DoesNotContain("WpfDispatcherUiThread", text, StringComparison.Ordinal);
        Assert.DoesNotContain("new InMemorySecretStore", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Central_version_is_unchanged_by_macos_host()
    {
        var xml = XDocument.Load(Path.Combine(FindRepoRoot(), "Directory.Build.props"));
        var version = xml.Root?
            .Elements("PropertyGroup")
            .Elements("Version")
            .Select(e => e.Value.Trim())
            .FirstOrDefault();
        Assert.Equal("1.2.0", version);
    }

    private static string ReadProject(string relative)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relative));
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
