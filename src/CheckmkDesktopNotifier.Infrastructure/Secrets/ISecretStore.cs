namespace CheckmkDesktopNotifier.Infrastructure.Secrets;

public interface ISecretStore
{
    void Save(string key, string secret);

    string? Read(string key);

    void Delete(string key);
}

public static class SecretStoreKeys
{
    public const string AutomationSecret = "CheckmkDesktopNotifier";
}
