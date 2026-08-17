namespace CheckmkDesktopNotifier.Core.Autostart;

/// <summary>
/// Per-user HKCU Run registration shared with a future installer. One value name, no HKLM.
/// </summary>
public static class AutostartCommand
{
    public const string Hive = "HKEY_CURRENT_USER";

    public const string SubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string ValueName = "CheckmkDesktopNotifier";

    public static string Format(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var trimmed = executablePath.Trim();
        if (trimmed.IndexOfAny(['\0', '\n', '\r']) >= 0)
        {
            throw new ArgumentException("Executable path must not contain control characters.", nameof(executablePath));
        }

        return "\"" + trimmed.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public static string Unquote(string commandLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);
        var trimmed = commandLine.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            return trimmed[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
        }

        return trimmed;
    }

    public static bool ContainsDisallowedPayload(string commandLine) =>
        commandLine.Contains("Authorization", StringComparison.OrdinalIgnoreCase)
        || commandLine.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
        || commandLine.Contains("CHECKMK_SECRET", StringComparison.OrdinalIgnoreCase)
        || commandLine.Contains("automation_secret", StringComparison.OrdinalIgnoreCase);
}
