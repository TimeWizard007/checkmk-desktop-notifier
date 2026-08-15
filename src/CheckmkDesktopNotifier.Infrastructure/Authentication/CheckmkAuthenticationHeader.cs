namespace CheckmkDesktopNotifier.Infrastructure.Authentication;

public static class CheckmkAuthenticationHeader
{
    public const string HeaderName = "Authorization";

    public static string CreateValue(string username, string secret)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username must not be empty.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret must not be empty.", nameof(secret));
        }

        return $"Bearer {username.Trim()} {secret.Trim()}";
    }
}
