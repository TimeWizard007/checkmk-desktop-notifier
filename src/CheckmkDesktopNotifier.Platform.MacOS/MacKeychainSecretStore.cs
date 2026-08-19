using CheckmkDesktopNotifier.Infrastructure.Secrets;

namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// <see cref="ISecretStore"/> backed by macOS Keychain. Does not fall back to
/// plaintext files or <c>InMemorySecretStore</c>.
/// </summary>
public sealed class MacKeychainSecretStore : ISecretStore
{
    private readonly IMacKeychain _keychain;

    public MacKeychainSecretStore()
        : this(new SecurityFrameworkKeychain())
    {
    }

    public MacKeychainSecretStore(IMacKeychain keychain)
    {
        _keychain = keychain ?? throw new ArgumentNullException(nameof(keychain));
    }

    public void Save(string key, string secret)
    {
        var account = MacKeychainItem.AccountFor(key);
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret must not be empty.", nameof(secret));
        }

        try
        {
            _keychain.SetPassword(MacKeychainItem.ServiceName, account, secret);
        }
        catch (Exception ex) when (ex is not ArgumentException and not PlatformNotSupportedException)
        {
            throw new InvalidOperationException("The macOS Keychain could not save the automation secret.");
        }
    }

    public string? Read(string key)
    {
        var account = MacKeychainItem.AccountFor(key);
        try
        {
            return _keychain.GetPassword(MacKeychainItem.ServiceName, account);
        }
        catch (Exception ex) when (ex is not ArgumentException and not PlatformNotSupportedException)
        {
            throw new InvalidOperationException("The macOS Keychain could not read the automation secret.");
        }
    }

    public void Delete(string key)
    {
        var account = MacKeychainItem.AccountFor(key);
        try
        {
            _keychain.DeletePassword(MacKeychainItem.ServiceName, account);
        }
        catch (Exception ex) when (ex is not ArgumentException and not PlatformNotSupportedException)
        {
            throw new InvalidOperationException("The macOS Keychain could not delete the automation secret.");
        }
    }
}
