using CheckmkDesktopNotifier.Core.Storage;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Secrets;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class UserSettingsAndSecretStoreTests
{
    [Fact]
    public void Settings_json_does_not_contain_secret()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-settings-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new JsonUserSettingsStore(path);
            store.Save(new UserSettings
            {
                BaseUrl = "https://checkmk.example.com",
                Site = "mysite",
                Username = "automation",
                PollIntervalSeconds = 60
            });

            var json = File.ReadAllText(path);
            Assert.DoesNotContain("Secret", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(TestOptions.Secret, json, StringComparison.Ordinal);
            Assert.DoesNotContain("Authorization", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("https://checkmk.example.com", json, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Settings_load_rejects_secret_property()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-settings-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, """{"baseUrl":"https://checkmk.example.com","secret":"nope"}""");
            var store = new JsonUserSettingsStore(path);
            Assert.Throws<InvalidOperationException>(() => store.Load());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void In_memory_secret_store_save_read_delete()
    {
        var store = new InMemorySecretStore();
        store.Save(SecretStoreKeys.AutomationSecret, TestOptions.Secret);
        Assert.Equal(TestOptions.Secret, store.Read(SecretStoreKeys.AutomationSecret));
        store.Delete(SecretStoreKeys.AutomationSecret);
        Assert.Null(store.Read(SecretStoreKeys.AutomationSecret));
    }

    [Fact]
    public void Gui_configuration_reset_removes_settings_and_secret()
    {
        var settings = new InMemoryUserSettingsStore();
        var secrets = new InMemorySecretStore();
        var gui = new GuiConfigurationService(settings, secrets);
        gui.Save(new UserSettings { BaseUrl = "https://checkmk.example.com", Site = "mysite", Username = "u", PollIntervalSeconds = 60 }, TestOptions.Secret);
        Assert.True(gui.HasStoredSecret);
        gui.Reset();
        Assert.False(settings.Exists);
        Assert.False(gui.HasStoredSecret);
    }

    [Fact]
    public void Changing_credentials_replaces_secret_without_writing_it_to_settings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-settings-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var settings = new JsonUserSettingsStore(path);
            var secrets = new InMemorySecretStore();
            var gui = new GuiConfigurationService(settings, secrets);
            gui.Save(new UserSettings { BaseUrl = "https://checkmk.example.com", Site = "mysite", Username = "u", PollIntervalSeconds = 60 }, "old-secret");
            gui.Save(new UserSettings { BaseUrl = "https://checkmk.example.com", Site = "mysite", Username = "u", PollIntervalSeconds = 45 }, "new-secret");
            Assert.Equal("new-secret", secrets.Read(SecretStoreKeys.AutomationSecret));
            var json = File.ReadAllText(path);
            Assert.DoesNotContain("old-secret", json, StringComparison.Ordinal);
            Assert.DoesNotContain("new-secret", json, StringComparison.Ordinal);
            Assert.Contains("45", json, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void App_storage_paths_use_supplied_user_data_directory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cdn-app-storage", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = AppStoragePaths.For(new ExplicitUserDataDirectory(directory));
            Assert.Equal(directory, paths.AppDataDirectory);
            Assert.True(Directory.Exists(directory));
            Assert.Equal(Path.Combine(directory, "settings.json"), paths.SettingsPath);
            Assert.DoesNotContain("Programs", paths.AppDataDirectory, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
