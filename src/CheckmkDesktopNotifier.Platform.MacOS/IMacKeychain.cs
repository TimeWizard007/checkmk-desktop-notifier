namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// Lowest-level generic-password Keychain operations. Production uses
/// <see cref="SecurityFrameworkKeychain"/>; tests use an in-process fake.
/// </summary>
public interface IMacKeychain
{
    void SetPassword(string service, string account, string secret);

    string? GetPassword(string service, string account);

    void DeletePassword(string service, string account);
}
