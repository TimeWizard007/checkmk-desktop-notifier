using CheckmkDesktopNotifier.App.MacOS;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Autostart;
using CheckmkDesktopNotifier.Core.Navigation;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Core.Threading;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Secrets;

namespace CheckmkDesktopNotifier.App.MacOS.Tests;

public sealed class MacSettingsViewModelTests
{
    [Fact]
    public async Task Connection_save_does_not_write_secret_into_json()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cdn-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var paths = new AppStoragePaths(directory);
        var secrets = new InMemorySecretStore();
        var store = new JsonUserSettingsStore(paths.SettingsPath);
        var gui = new GuiConfigurationService(store, secrets);
        var prefs = new JsonUserPreferencesStore(paths.PreferencesPath);
        var vm = Create(gui, prefs);

        vm.SelectConnectionCommand.Execute(null);
        Assert.True(vm.IsConnectionSection);
        vm.BaseUrl = "https://checkmk.example.invalid";
        vm.Site = "itssrv";
        vm.Username = "automation";
        vm.Secret = "super-secret-value";
        vm.PollIntervalText = "60";
        await vm.SaveCommand.ExecuteAsync(null);

        var json = File.ReadAllText(paths.SettingsPath);
        Assert.DoesNotContain("super-secret-value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("super-secret-value", secrets.Read(SecretStoreKeys.AutomationSecret));
    }

    [Fact]
    public void General_section_exposes_take_and_login_copy()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cdn-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var paths = new AppStoragePaths(directory);
        var vm = Create(
            new GuiConfigurationService(new JsonUserSettingsStore(paths.SettingsPath), new InMemorySecretStore()),
            new JsonUserPreferencesStore(paths.PreferencesPath));
        vm.SelectGeneralCommand.Execute(null);
        Assert.True(vm.IsGeneralSection);
        vm.SelectNotificationsCommand.Execute(null);
        Assert.True(vm.IsNotificationsSection);
        vm.SelectGeneralCommand.Execute(null);
        Assert.True(vm.IsGeneralSection);
        Assert.Contains("macOS user", vm.TeamHint, StringComparison.Ordinal);
        Assert.Contains("LaunchAgent", vm.LoginItemHint, StringComparison.Ordinal);
        vm.EnableTake = true;
        vm.TakeDisplayName = "Michał";
        Assert.True(vm.EnableTake);
    }

    private static MacConnectionViewModel Create(GuiConfigurationService gui, IUserPreferences prefs)
    {
        var loaded = new LoadedConfiguration
        {
            Options = new CheckmkOptions { Mode = ClientMode.Real, PollIntervalSeconds = 60 },
            Source = ConfigurationSource.None,
            IsUsableReal = false,
            IsMock = false
        };
        return new MacConnectionViewModel(
            gui,
            new CheckmkConnectionTester(),
            new FakeCoordinator(),
            new FakePoller(),
            new AlertStateService(new InMemoryAlertStateStore(), TimeProvider.System),
            ImmediateUiThread.Instance,
            new FakeLauncher(),
            loaded,
            preferences: prefs,
            autostart: new AutostartService(new InMemoryAutostartStore(), new CurrentProcessExecutable()));
    }

    private sealed class FakeCoordinator : IMonitoringCoordinator
    {
        public CheckmkOptions? CurrentOptions { get; private set; }
        public ConnectionIdentity? ActiveIdentity => null;
        public bool IsPollingEnabled { get; private set; }
        public Task ApplyAsync(CheckmkOptions options, CancellationToken cancellationToken = default)
        {
            CurrentOptions = options;
            IsPollingEnabled = true;
            return Task.CompletedTask;
        }
        public Task ResetPollingAsync()
        {
            IsPollingEnabled = false;
            return Task.CompletedTask;
        }
        public Task RunPollingAsync(IProblemPoller poller, CancellationToken stoppingToken) => Task.CompletedTask;
    }

    private sealed class FakePoller : IProblemPoller
    {
        public ConnectionStatus Status { get; } = ConnectionStatus.Idle;
        public TimeSpan Interval { get; private set; } = TimeSpan.FromSeconds(60);
        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefreshWhenIdleAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RunLoopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void SetInterval(TimeSpan interval) => Interval = interval;
    }

    private sealed class FakeLauncher : IUriLauncher
    {
        public void Open(Uri uri)
        {
        }
    }
}
