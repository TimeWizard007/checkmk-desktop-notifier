using CheckmkDesktopNotifier.Infrastructure.Secrets;
using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.Platform.MacOS.Tests;

public sealed class MacKeychainSecretStoreTests
{
    [Fact]
    public void Item_identity_is_stable_product_service_and_store_key_account()
    {
        Assert.Equal("CheckmkDesktopNotifier", MacKeychainItem.ServiceName);
        Assert.Equal(
            SecretStoreKeys.AutomationSecret,
            MacKeychainItem.AccountFor(SecretStoreKeys.AutomationSecret));
        Assert.Equal("CheckmkDesktopNotifier", SecretStoreKeys.AutomationSecret);
    }

    [Fact]
    public void AccountFor_rejects_empty_key()
    {
        Assert.Throws<ArgumentException>(() => MacKeychainItem.AccountFor(" "));
    }

    [Fact]
    public void Save_read_delete_use_service_and_account_without_touching_native_keychain()
    {
        var keychain = new FakeMacKeychain();
        var store = new MacKeychainSecretStore(keychain);

        store.Save(SecretStoreKeys.AutomationSecret, "not-a-real-secret");
        Assert.Equal("not-a-real-secret", store.Read(SecretStoreKeys.AutomationSecret));
        Assert.Equal(MacKeychainItem.ServiceName, keychain.LastService);
        Assert.Equal(SecretStoreKeys.AutomationSecret, keychain.LastAccount);

        store.Delete(SecretStoreKeys.AutomationSecret);
        Assert.Null(store.Read(SecretStoreKeys.AutomationSecret));
    }

    [Fact]
    public void Save_rejects_empty_secret_before_keychain()
    {
        var keychain = new FakeMacKeychain();
        var store = new MacKeychainSecretStore(keychain);
        Assert.Throws<ArgumentException>(() => store.Save(SecretStoreKeys.AutomationSecret, " "));
        Assert.Null(keychain.LastService);
    }

    [Fact]
    public void Exceptions_do_not_include_the_secret()
    {
        var store = new MacKeychainSecretStore(new ThrowingMacKeychain());
        var thrown = Assert.Throws<InvalidOperationException>(
            () => store.Save(SecretStoreKeys.AutomationSecret, "super-secret-value"));
        Assert.DoesNotContain("super-secret-value", thrown.Message, StringComparison.Ordinal);
        Assert.Null(thrown.InnerException);
    }

    [Fact]
    public void Security_framework_keychain_is_gated_off_macos()
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }

        var keychain = new SecurityFrameworkKeychain();
        var ex = Assert.Throws<PlatformNotSupportedException>(
            () => keychain.SetPassword(MacKeychainItem.ServiceName, "account", "not-logged-value"));
        Assert.Contains("macOS", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not-logged-value", ex.Message, StringComparison.Ordinal);
        Assert.Throws<PlatformNotSupportedException>(
            () => keychain.GetPassword(MacKeychainItem.ServiceName, "account"));
        Assert.Throws<PlatformNotSupportedException>(
            () => keychain.DeletePassword(MacKeychainItem.ServiceName, "account"));
    }

    private sealed class FakeMacKeychain : IMacKeychain
    {
        private readonly Dictionary<(string Service, string Account), string> _items = new();

        public string? LastService { get; private set; }

        public string? LastAccount { get; private set; }

        public void SetPassword(string service, string account, string secret)
        {
            LastService = service;
            LastAccount = account;
            _items[(service, account)] = secret;
        }

        public string? GetPassword(string service, string account)
        {
            LastService = service;
            LastAccount = account;
            return _items.TryGetValue((service, account), out var secret) ? secret : null;
        }

        public void DeletePassword(string service, string account)
        {
            LastService = service;
            LastAccount = account;
            _items.Remove((service, account));
        }
    }

    private sealed class ThrowingMacKeychain : IMacKeychain
    {
        public void SetPassword(string service, string account, string secret) =>
            throw new InvalidOperationException("native failed with " + secret);

        public string? GetPassword(string service, string account) =>
            throw new InvalidOperationException("native failed");

        public void DeletePassword(string service, string account) =>
            throw new InvalidOperationException("native failed");
    }
}
