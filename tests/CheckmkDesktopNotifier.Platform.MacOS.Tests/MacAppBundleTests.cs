using CheckmkDesktopNotifier.Core.Autostart;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Notifications;

namespace CheckmkDesktopNotifier.Platform.MacOS.Tests;

public sealed class MacAppBundleTests
{
    [Fact]
    public void Raw_executable_is_not_a_bundle_and_must_not_call_user_notification_center()
    {
        var layout = MacAppBundleLayout.Detect("/tmp/cdn-m3-macos-x64/CheckmkDesktopNotifier.MacOS");
        Assert.False(layout.IsApplicationBundle);
        Assert.Null(layout.BundleIdentifier);
        Assert.False(MacNotificationEnvironment.ShouldCallCurrentNotificationCenter(layout.BundleIdentifier));
        Assert.Equal(
            MacNotificationBackend.Disabled,
            MacNotificationEnvironment.SelectBackend(isMacOS: true, liveBundleIdentifier: null, layout));
        Assert.Equal(
            MacNotificationBackend.Recording,
            MacNotificationEnvironment.SelectBackend(isMacOS: false, liveBundleIdentifier: null, layout));
    }

    [Fact]
    public void Bundled_layout_reads_identifier_and_launch_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "cdn-bundle-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, MacAppBundleLayout.AppFolderName);
        Directory.CreateDirectory(Path.Combine(app, "Contents", "MacOS"));
        File.WriteAllText(Path.Combine(app, "Contents", "Info.plist"), MacAppInfoPlist.BuildXml(MacAppBundleLayout.ProductVersion));
        var exe = Path.Combine(app, "Contents", "MacOS", MacAppBundleLayout.ExecutableName);
        File.WriteAllText(exe, "placeholder");

        var layout = MacAppBundleLayout.Detect(exe);
        Assert.True(layout.IsApplicationBundle);
        Assert.Equal(app, layout.BundlePath);
        Assert.Equal(MacAppBundleLayout.Identifier, layout.BundleIdentifier);
        Assert.Equal(app, layout.LaunchPath);
        Assert.True(MacNotificationEnvironment.ShouldCallCurrentNotificationCenter(layout.BundleIdentifier));
        Assert.Equal(
            MacNotificationBackend.Native,
            MacNotificationEnvironment.SelectBackend(isMacOS: true, liveBundleIdentifier: layout.BundleIdentifier, layout));
    }

    [Fact]
    public void Empty_bundle_identifier_does_not_allow_current_notification_center()
    {
        Assert.False(MacNotificationEnvironment.ShouldCallCurrentNotificationCenter(null));
        Assert.False(MacNotificationEnvironment.ShouldCallCurrentNotificationCenter(""));
        Assert.False(MacNotificationEnvironment.ShouldCallCurrentNotificationCenter("   "));
        Assert.True(MacNotificationEnvironment.ShouldCallCurrentNotificationCenter(MacAppBundleLayout.Identifier));
    }

    [Fact]
    public void Factory_create_does_not_throw_outside_a_bundle()
    {
        var created = MacNotificationFactory.Create();
        Assert.NotNull(created);
        created.Show(new IncidentAlert
        {
            ObjectId = CheckmkDesktopNotifier.Core.Domain.MonitoredObjectId.Host(
                new CheckmkDesktopNotifier.Core.Domain.SiteId("itssrv"),
                "web01"),
            Severity = CheckmkDesktopNotifier.Core.Domain.Severity.Critical,
            Title = "Checkmk Desktop Notifier",
            Body = "HOST DOWN"
        });
        Assert.False(NotifyObjC.HasMainBundleIdentifier());
        Assert.False(NotifyObjC.RequestModernAuthorization());
    }

    [Fact]
    public void Native_backend_degrades_when_not_running_on_macos()
    {
        var service = MacNotificationFactory.Create(MacNotificationBackend.Native);
        Assert.NotNull(service);
        if (!OperatingSystem.IsMacOS())
        {
            Assert.IsType<DisabledMacNotificationService>(service);
        }
    }

    [Fact]
    public void Committed_info_plist_matches_bundle_identifier()
    {
        var xml = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src/CheckmkDesktopNotifier.App.MacOS/Bundle/Info.plist"));
        Assert.Equal(MacAppBundleLayout.Identifier, MacAppInfoPlist.TryReadIdentifier(xml));
        Assert.Contains(MacAppBundleLayout.ExecutableName, xml, StringComparison.Ordinal);
        Assert.Contains(MacAppBundleLayout.ProductVersion, xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Info_plist_contains_stable_bundle_identity()
    {
        var xml = MacAppInfoPlist.BuildXml(MacAppBundleLayout.ProductVersion);
        Assert.Equal(MacAppBundleLayout.Identifier, MacAppInfoPlist.TryReadIdentifier(xml));
        Assert.Contains(MacAppBundleLayout.ExecutableName, xml, StringComparison.Ordinal);
        Assert.Contains("LSUIElement", xml, StringComparison.Ordinal);
        Assert.Contains(MacAppBundleLayout.ProductVersion, xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Packager_writes_app_structure_without_nesting_another_app()
    {
        var publish = Path.Combine(Path.GetTempPath(), "cdn-pub-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(publish, MacAppBundleLayout.AppFolderName);
        Directory.CreateDirectory(publish);
        File.WriteAllText(Path.Combine(publish, MacAppBundleLayout.ExecutableName), "exe");
        File.WriteAllText(Path.Combine(publish, "readme.txt"), "keep");
        Directory.CreateDirectory(Path.Combine(publish, "nested.app"));

        MacAppBundlePackager.Package(publish, app, MacAppBundleLayout.ProductVersion);

        Assert.True(File.Exists(Path.Combine(app, "Contents", "Info.plist")));
        Assert.True(File.Exists(Path.Combine(app, "Contents", "MacOS", MacAppBundleLayout.ExecutableName)));
        Assert.True(Directory.Exists(Path.Combine(app, "Contents", "Resources")));
        Assert.False(Directory.Exists(Path.Combine(app, "Contents", "MacOS", "nested.app")));
        Assert.Equal(
            MacAppBundleLayout.Identifier,
            MacAppInfoPlist.TryReadIdentifier(File.ReadAllText(Path.Combine(app, "Contents", "Info.plist"))));
    }

    [Fact]
    public void Launch_agent_for_app_bundle_uses_open_on_the_app_path()
    {
        var exe = "/Users/mwi/Applications/Checkmk Desktop Notifier.app/Contents/MacOS/CheckmkDesktopNotifier.MacOS";
        var xml = MacLaunchAgentPlist.Build(exe);
        Assert.Contains(MacOpenCommand.Executable, xml, StringComparison.Ordinal);
        Assert.Contains("Checkmk Desktop Notifier.app", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("Contents/MacOS", xml, StringComparison.Ordinal);
        Assert.Equal(
            "/Users/mwi/Applications/Checkmk Desktop Notifier.app",
            MacLaunchAgentPlist.TryReadExecutable(xml));
        Assert.DoesNotContain("Bearer", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Launch_agent_store_writes_open_command_for_bundled_executable()
    {
        var path = Path.Combine(Path.GetTempPath(), "cdn-agent-" + Guid.NewGuid().ToString("N") + ".plist");
        var store = new MacLaunchAgentAutostartStore(path);
        var exe = "/opt/Checkmk Desktop Notifier.app/Contents/MacOS/CheckmkDesktopNotifier.MacOS";
        var autostart = new AutostartService(store, new FixedExecutable(exe));
        Assert.True(autostart.SetEnabled(true).Succeeded);
        var xml = File.ReadAllText(path);
        Assert.Contains("/usr/bin/open", xml, StringComparison.Ordinal);
        Assert.Equal("/opt/Checkmk Desktop Notifier.app", AutostartCommand.Unquote(store.Read()!.CommandLine));
    }

    [Fact]
    public void Current_notification_center_source_is_gated_on_bundle_identifier()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src/CheckmkDesktopNotifier.Platform.MacOS/NativeMacNotificationService.cs"));
        var request = source.IndexOf("public static bool RequestModernAuthorization", StringComparison.Ordinal);
        var gate = source.IndexOf("HasMainBundleIdentifier", request, StringComparison.Ordinal);
        var call = source.IndexOf("currentNotificationCenter", request, StringComparison.Ordinal);
        Assert.True(request >= 0);
        Assert.True(gate > request);
        Assert.True(call > gate);
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

    private sealed class FixedExecutable : IApplicationExecutable
    {
        private readonly string _path;
        public FixedExecutable(string path) => _path = path;
        public string GetPath() => _path;
    }
}
