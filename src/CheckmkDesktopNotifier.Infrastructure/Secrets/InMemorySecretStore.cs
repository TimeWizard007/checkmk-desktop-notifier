using System.Collections.Concurrent;

namespace CheckmkDesktopNotifier.Infrastructure.Secrets;

public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

    public void Save(string key, string secret)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key must not be empty.", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret must not be empty.", nameof(secret));
        }

        _values[key] = secret;
    }

    public string? Read(string key) =>
        _values.TryGetValue(key, out var secret) ? secret : null;

    public void Delete(string key) => _values.TryRemove(key, out _);
}
