namespace CheckmkDesktopNotifier.Core.Acknowledgements;

public static class TakeDisplayName
{
    public const int MaxLength = 64;

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxLength)
        {
            return null;
        }

        foreach (var c in trimmed)
        {
            if (char.IsControl(c))
            {
                return null;
            }
        }

        return trimmed;
    }

    public static string Clamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        foreach (var c in trimmed)
        {
            if (char.IsControl(c))
            {
                return string.Empty;
            }
        }

        return trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength];
    }

    public static string SuggestFromUserName()
    {
        try
        {
            return Clamp(Environment.UserName);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
