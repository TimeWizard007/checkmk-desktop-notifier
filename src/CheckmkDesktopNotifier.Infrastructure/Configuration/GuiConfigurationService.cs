using CheckmkDesktopNotifier.Infrastructure.Secrets;

namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public sealed class GuiConfigurationService
{
    private readonly IUserSettingsStore _settings;
    private readonly ISecretStore _secrets;

    public GuiConfigurationService(IUserSettingsStore settings, ISecretStore secrets)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    public bool HasStoredSecret
    {
        get
        {
            try
            {
                return !string.IsNullOrWhiteSpace(_secrets.Read(SecretStoreKeys.AutomationSecret));
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public UserSettings? LoadSettings() => _settings.Exists ? _settings.Load() : null;

    public string? ResolveSecret(string? typed)
    {
        if (!string.IsNullOrWhiteSpace(typed))
        {
            return typed.Trim();
        }

        try
        {
            return _secrets.Read(SecretStoreKeys.AutomationSecret);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Save(UserSettings settings, string? secret)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings.Save(settings);
        if (!string.IsNullOrWhiteSpace(secret))
        {
            _secrets.Save(SecretStoreKeys.AutomationSecret, secret);
        }
    }

    public void Reset()
    {
        _settings.Delete();
        _secrets.Delete(SecretStoreKeys.AutomationSecret);
    }
}
