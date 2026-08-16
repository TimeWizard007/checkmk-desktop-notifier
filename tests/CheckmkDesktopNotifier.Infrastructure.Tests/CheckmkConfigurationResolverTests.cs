using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Secrets;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

[Collection("Environment")]
public sealed class CheckmkConfigurationResolverTests
{
    [Fact]
    public void First_run_with_no_configuration_is_unconfigured()
    {
        using var env = new EnvironmentScope();
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-resolver-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previous = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(directory);
        try
        {
            var loaded = CheckmkConfigurationResolver.Resolve(
                new AppStoragePaths(directory),
                new JsonUserSettingsStore(Path.Combine(directory, "settings.json")),
                new InMemorySecretStore(),
                developerOptions: new CheckmkOptions(),
                discoveredConfigPath: null);

            Assert.Equal(ConfigurationSource.None, loaded.Source);
            Assert.False(loaded.IsUsableReal);
            Assert.False(loaded.IsMock);
            Assert.True(loaded.NeedsFirstRunSetup);
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Gui_settings_win_over_environment_variables()
    {
        using var env = new EnvironmentScope();
        env.Set("CHECKMK_BASE_URL", "https://from-env.example.invalid");
        env.Set("CHECKMK_SITE", "envsite");
        env.Set("CHECKMK_SECRET", "env-secret");

        var settings = new InMemoryUserSettingsStore
        {
            Current = new UserSettings
            {
                BaseUrl = "https://from-gui.example.invalid",
                Site = "guisite",
                Username = "guiuser",
                PollIntervalSeconds = 30
            }
        };
        var secrets = new InMemorySecretStore();
        secrets.Save(SecretStoreKeys.AutomationSecret, TestOptions.Secret);

        var loaded = CheckmkConfigurationResolver.Resolve(
            new AppStoragePaths(Path.GetTempPath()),
            settings,
            secrets);

        Assert.Equal(ConfigurationSource.Gui, loaded.Source);
        Assert.Equal("https://from-gui.example.invalid", loaded.Options.BaseUrl);
        Assert.Equal("guisite", loaded.Options.Site);
        Assert.Equal(TestOptions.Secret, loaded.Options.Secret);
        Assert.NotEqual("env-secret", loaded.Options.Secret);
        Assert.True(loaded.IsUsableReal);
    }

    [Fact]
    public void Explicit_checkmk_config_overrides_gui()
    {
        using var env = new EnvironmentScope();
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-resolver-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "dev.json");
        File.WriteAllText(configPath, """{"Mode":"Real","BaseUrl":"https://from-file.example.invalid","Site":"filesite","Username":"fileuser","Secret":"file-secret","PollIntervalSeconds":60}""");
        env.Set("CHECKMK_CONFIG", configPath);

        var settings = new InMemoryUserSettingsStore
        {
            Current = new UserSettings
            {
                BaseUrl = "https://from-gui.example.invalid",
                Site = "guisite",
                Username = "guiuser",
                PollIntervalSeconds = 30
            }
        };

        try
        {
            var loaded = CheckmkConfigurationResolver.Resolve(
                new AppStoragePaths(directory),
                settings,
                new InMemorySecretStore());

            Assert.Equal(ConfigurationSource.ExplicitFile, loaded.Source);
            Assert.Equal("filesite", loaded.Options.Site);
            Assert.Equal("file-secret", loaded.Options.Secret);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Mock_mode_does_not_read_production_secret_store()
    {
        using var env = new EnvironmentScope();
        env.Set("CHECKMK_MODE", "Mock");
        var secrets = new InMemorySecretStore();
        secrets.Save(SecretStoreKeys.AutomationSecret, TestOptions.Secret);
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-resolver-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previous = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(directory);
        try
        {
            var loaded = CheckmkConfigurationResolver.Resolve(
                new AppStoragePaths(directory),
                new InMemoryUserSettingsStore(),
                secrets,
                developerOptions: CheckmkOptionsLoader.ApplyEnvironment(new CheckmkOptions()),
                discoveredConfigPath: null);

            Assert.True(loaded.IsMock);
            Assert.False(loaded.IsUsableReal);
            Assert.NotEqual(TestOptions.Secret, loaded.Options.Secret);
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Malformed_gui_settings_do_not_delete_alert_state()
    {
        using var env = new EnvironmentScope();
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-resolver-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var settingsPath = Path.Combine(directory, "settings.json");
        var alertPath = Path.Combine(directory, "alert-state.json");
        File.WriteAllText(settingsPath, "{ not json");
        File.WriteAllText(alertPath, """{"schemaVersion":1,"incidents":[]}""");

        try
        {
            var loaded = CheckmkConfigurationResolver.Resolve(
                new AppStoragePaths(directory),
                new JsonUserSettingsStore(settingsPath),
                new InMemorySecretStore());

            Assert.False(loaded.IsUsableReal);
            Assert.Equal(ConfigurationSource.Gui, loaded.Source);
            Assert.NotNull(loaded.LoadError);
            Assert.True(File.Exists(alertPath));
            Assert.Contains("schemaVersion", File.ReadAllText(alertPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Reopening_saved_gui_configuration_loads_secret()
    {
        using var env = new EnvironmentScope();
        var settings = new InMemoryUserSettingsStore();
        var secrets = new InMemorySecretStore();
        var gui = new GuiConfigurationService(settings, secrets);
        gui.Save(
            new UserSettings
            {
                BaseUrl = "https://checkmk.example.invalid",
                Site = "mysite",
                Username = "automation",
                PollIntervalSeconds = 60
            },
            TestOptions.Secret);

        var loaded = CheckmkConfigurationResolver.Resolve(
            new AppStoragePaths(Path.GetTempPath()),
            settings,
            secrets);

        Assert.True(loaded.IsUsableReal);
        Assert.Equal("mysite", loaded.Options.Site);
        Assert.Equal(TestOptions.Secret, loaded.Options.Secret);
    }
}
