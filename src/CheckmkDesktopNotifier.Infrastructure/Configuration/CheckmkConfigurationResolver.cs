using System.Text.Json;
using CheckmkDesktopNotifier.Infrastructure.Secrets;

namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public enum ConfigurationSource
{
    None = 0,
    Gui = 1,
    ExplicitFile = 2,
    DiscoveredFile = 3,
    Environment = 4
}

public sealed class LoadedConfiguration
{
    public required CheckmkOptions Options { get; init; }

    public required ConfigurationSource Source { get; init; }

    public bool IsUsableReal { get; init; }

    public bool IsMock { get; init; }

    public string? LoadError { get; init; }

    public ConnectionIdentity? Identity { get; init; }

    public bool NeedsFirstRunSetup => !IsMock && !IsUsableReal;
}

public static class CheckmkConfigurationResolver
{
    public static LoadedConfiguration Resolve(
        AppStoragePaths paths,
        IUserSettingsStore settingsStore,
        ISecretStore secretStore,
        CheckmkOptions? developerOptions = null,
        string? discoveredConfigPath = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(secretStore);

        var explicitPath = Environment.GetEnvironmentVariable("CHECKMK_CONFIG");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return FromDeveloperFile(explicitPath.Trim(), ConfigurationSource.ExplicitFile, secretStore);
        }

        if (settingsStore.Exists)
        {
            return FromGui(settingsStore, secretStore);
        }

        var discovered = developerOptions ?? CheckmkOptionsLoader.Load();
        var discoveredPath = discoveredConfigPath ?? CheckmkOptionsLoader.ResolveConfigPath();
        var mockRequested = string.Equals(
            Environment.GetEnvironmentVariable("CHECKMK_MODE"),
            "Mock",
            StringComparison.OrdinalIgnoreCase);

        if (discovered.Mode == ClientMode.Mock && (discoveredPath is not null || mockRequested))
        {
            return FromOptions(discovered, discoveredPath is not null ? ConfigurationSource.DiscoveredFile : ConfigurationSource.Environment, secretStore);
        }

        if (discovered.Mode == ClientMode.Real
            || !string.IsNullOrWhiteSpace(discovered.BaseUrl)
            || !string.IsNullOrWhiteSpace(discovered.Site)
            || !string.IsNullOrWhiteSpace(discovered.Username)
            || !string.IsNullOrWhiteSpace(discovered.Secret))
        {
            var source = discoveredPath is not null ? ConfigurationSource.DiscoveredFile : ConfigurationSource.Environment;
            return FromOptions(discovered, source, secretStore);
        }

        return new LoadedConfiguration
        {
            Options = new CheckmkOptions { Mode = ClientMode.Real, PollIntervalSeconds = CheckmkOptions.DefaultPollIntervalSeconds },
            Source = ConfigurationSource.None,
            IsUsableReal = false,
            IsMock = false
        };
    }

    private static LoadedConfiguration FromGui(IUserSettingsStore settingsStore, ISecretStore secretStore)
    {
        UserSettings settings;
        try
        {
            settings = settingsStore.Load()
                       ?? throw new InvalidOperationException("Settings file is empty.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or IOException)
        {
            return new LoadedConfiguration
            {
                Options = new CheckmkOptions { Mode = ClientMode.Real, PollIntervalSeconds = CheckmkOptions.DefaultPollIntervalSeconds },
                Source = ConfigurationSource.Gui,
                IsUsableReal = false,
                IsMock = false,
                LoadError = "Saved settings could not be read. Open Settings to repair the configuration."
            };
        }

        string? secret;
        try
        {
            secret = secretStore.Read(SecretStoreKeys.AutomationSecret);
        }
        catch (Exception)
        {
            return new LoadedConfiguration
            {
                Options = settings.ToOptions(secret: null),
                Source = ConfigurationSource.Gui,
                IsUsableReal = false,
                IsMock = false,
                LoadError = "The saved automation secret could not be read from Windows Credential Manager."
            };
        }

        var options = settings.ToOptions(secret);
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            try
            {
                options = new CheckmkOptions
                {
                    Mode = ClientMode.Real,
                    BaseUrl = CheckmkOptionsValidator.NormalizeBaseUrl(options.BaseUrl),
                    Site = options.Site,
                    Username = options.Username,
                    Secret = options.Secret,
                    PollIntervalSeconds = options.PollIntervalSeconds
                };
            }
            catch (CheckmkOptionsValidationException ex)
            {
                return new LoadedConfiguration
                {
                    Options = options,
                    Source = ConfigurationSource.Gui,
                    IsUsableReal = false,
                    IsMock = false,
                    LoadError = ex.Message
                };
            }
        }

        var usable = CheckmkOptionsValidator.TryValidate(options, out _);
        ConnectionIdentity? identity = null;
        if (!string.IsNullOrWhiteSpace(options.BaseUrl) && !string.IsNullOrWhiteSpace(options.Site))
        {
            try
            {
                identity = ConnectionIdentity.From(options.BaseUrl, options.Site);
            }
            catch (CheckmkOptionsValidationException)
            {
                identity = null;
            }
        }

        return new LoadedConfiguration
        {
            Options = options,
            Source = ConfigurationSource.Gui,
            IsUsableReal = usable,
            IsMock = false,
            Identity = identity,
            LoadError = usable ? null : (string.IsNullOrWhiteSpace(secret)
                ? "The automation secret is missing. Open Settings to enter it."
                : null)
        };
    }

    private static LoadedConfiguration FromDeveloperFile(
        string path,
        ConfigurationSource source,
        ISecretStore secretStore)
    {
        if (!File.Exists(path))
        {
            return new LoadedConfiguration
            {
                Options = new CheckmkOptions { Mode = ClientMode.Real },
                Source = source,
                IsUsableReal = false,
                IsMock = false,
                LoadError = "CHECKMK_CONFIG path was not found."
            };
        }

        CheckmkOptions options;
        try
        {
            options = CheckmkOptionsLoader.ApplyEnvironment(CheckmkOptionsLoader.FromJson(File.ReadAllText(path)));
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new LoadedConfiguration
            {
                Options = new CheckmkOptions { Mode = ClientMode.Real },
                Source = source,
                IsUsableReal = false,
                IsMock = false,
                LoadError = "Developer configuration could not be read."
            };
        }

        return FromOptions(options, source, secretStore);
    }

    private static LoadedConfiguration FromOptions(
        CheckmkOptions options,
        ConfigurationSource source,
        ISecretStore secretStore)
    {
        _ = secretStore;
        if (options.Mode == ClientMode.Mock)
        {
            return new LoadedConfiguration
            {
                Options = options,
                Source = source,
                IsUsableReal = false,
                IsMock = true
            };
        }

        var usable = CheckmkOptionsValidator.TryValidate(options, out _);
        ConnectionIdentity? identity = null;
        if (usable)
        {
            identity = ConnectionIdentity.From(options.BaseUrl!, options.Site!);
        }

        return new LoadedConfiguration
        {
            Options = options,
            Source = source,
            IsUsableReal = usable,
            IsMock = false,
            Identity = identity
        };
    }
}
