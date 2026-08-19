using CheckmkDesktopNotifier.App.MacOS;
using CheckmkDesktopNotifier.Core.Autostart;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Notifications;
using CheckmkDesktopNotifier.Core.Threading;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.App.MacOS.Tests;

public sealed class MacParityTests
{
    [Fact]
    public void Settings_section_state_switches()
    {
        var vm = new MacConfirmViewModel("t", "b", "Take", "Cancel");
        Assert.True(vm.ShowCancel);
        vm.AcceptCommand.Execute(null);
        Assert.True(vm.Result);
    }

    [Fact]
    public void Long_text_is_ellipsized()
    {
        Assert.Equal("abc", MacTextEllipsis.Fit("abc", 8));
        Assert.Equal("abcd…", MacTextEllipsis.Fit("abcdefghij", 5));
        Assert.Equal(string.Empty, MacTextEllipsis.Fit(null, 5));
    }

    [Fact]
    public void Mute_and_volume_persist_on_preferences()
    {
        var prefs = new JsonUserPreferencesStore(Path.Combine(Path.GetTempPath(), "cdn-prefs-" + Guid.NewGuid().ToString("N") + ".json"));
        prefs.SetMuteSound(true);
        prefs.SetVolumePercent(12);
        Assert.True(prefs.MuteSound);
        Assert.Equal(12, prefs.VolumePercent);
        MuteCommands.Toggle(prefs);
        Assert.False(prefs.MuteSound);
    }

    [Fact]
    public void Notification_delivery_failure_is_contained()
    {
        var inner = new RecordingMacNotificationService { ThrowOnShow = true };
        var guarded = new GuardedNotificationService(inner);
        guarded.Show(new IncidentAlert
        {
            ObjectId = MonitoredObjectId.Host(new SiteId("itssrv"), "web01"),
            Severity = Severity.Critical,
            Title = "Checkmk Desktop Notifier",
            Body = "HOST DOWN"
        });
        Assert.Equal(1, guarded.FailureCount);
    }

    [Fact]
    public void Notification_token_round_trips_host_and_service()
    {
        var host = MonitoredObjectId.Host(new SiteId("itssrv"), "web01");
        Assert.True(NativeMacNotificationService.TryDecode(NativeMacNotificationService.Encode(host), out var decodedHost));
        Assert.Equal("web01", decodedHost!.HostName);

        var service = MonitoredObjectId.Service(new SiteId("itssrv"), "web01", "CPU");
        Assert.True(NativeMacNotificationService.TryDecode(NativeMacNotificationService.Encode(service), out var decodedService));
        Assert.Equal("CPU", decodedService!.ServiceDescription);
    }

    [Fact]
    public void Launch_agent_plist_round_trips_without_secrets()
    {
        var xml = MacLaunchAgentPlist.Build("/tmp/CheckmkDesktopNotifier.MacOS");
        Assert.Contains(MacLoginItemCapability.Label, xml, StringComparison.Ordinal);
        Assert.Contains("<true/>", xml, StringComparison.Ordinal);
        Assert.Contains("RunAtLoad", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", xml, StringComparison.Ordinal);
        Assert.Equal("/tmp/CheckmkDesktopNotifier.MacOS", MacLaunchAgentPlist.TryReadExecutable(xml));
    }

    [Fact]
    public void Launch_agent_store_reflects_enable_disable()
    {
        var path = Path.Combine(Path.GetTempPath(), "cdn-agent-" + Guid.NewGuid().ToString("N") + ".plist");
        var store = new MacLaunchAgentAutostartStore(path);
        var autostart = new AutostartService(store, new FixedExecutable("/opt/cdn/CheckmkDesktopNotifier.MacOS"));
        Assert.False(autostart.IsEnabled);
        Assert.True(autostart.SetEnabled(true).Succeeded);
        Assert.True(autostart.IsEnabled);
        Assert.True(autostart.SetEnabled(false).Succeeded);
        Assert.False(autostart.IsEnabled);
        Assert.False(MacLoginItemCapability.RequiresAppBundle);
    }

    [Fact]
    public void Single_instance_second_start_signals_existing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cdn-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Assert.True(MacSingleInstanceLock.TryOwn(directory, out var first));
        Assert.NotNull(first);
        using (first)
        {
            Assert.False(MacSingleInstanceLock.TryOwn(directory, out _));
            var activated = 0;
            first.Listen(() => activated++);
            MacSingleInstanceLock.SignalExisting(directory);
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (activated == 0 && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(50);
            }

            Assert.Equal(1, activated);
        }
    }

    [Fact]
    public void Posted_notification_delivery_uses_ui_thread_and_stays_guarded()
    {
        var inner = new RecordingMacNotificationService { ThrowOnShow = true };
        var posted = new PostedNotificationService(
            ImmediateUiThread.Instance,
            new GuardedNotificationService(inner));
        posted.Show(new IncidentAlert
        {
            ObjectId = MonitoredObjectId.Host(new SiteId("itssrv"), "web01"),
            Severity = Severity.Critical,
            Title = "Checkmk Desktop Notifier",
            Body = "HOST DOWN"
        });
        Assert.Empty(inner.Shown);
    }

    [Fact]
    public void App_theme_follows_system_with_dark_and_light_dictionaries()
    {
        var axaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "src/CheckmkDesktopNotifier.App.MacOS/App.axaml"));
        Assert.Contains("RequestedThemeVariant=\"Default\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"Dark\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"Light\"", axaml, StringComparison.Ordinal);
        Assert.Contains("CdnPanelBackground", axaml, StringComparison.Ordinal);
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
