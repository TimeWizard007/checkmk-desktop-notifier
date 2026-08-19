namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// Stable Keychain generic-password identity for this application.
/// Service is always the product name; account is the <c>ISecretStore</c> key.
/// </summary>
public static class MacKeychainItem
{
    public const string ServiceName = "CheckmkDesktopNotifier";

    public static string AccountFor(string secretStoreKey)
    {
        if (string.IsNullOrWhiteSpace(secretStoreKey))
        {
            throw new ArgumentException("Key must not be empty.", nameof(secretStoreKey));
        }

        return secretStoreKey.Trim();
    }
}
