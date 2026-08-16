using System.Text.Json;
using System.Text.Json.Serialization;

namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public static class CheckmkOptionsLoader
{
    public const string FileName = "checkmk.local.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static CheckmkOptions Load()
    {
        var fromFile = ReadFile(ResolveConfigPath());
        return ApplyEnvironment(fromFile);
    }

    internal static CheckmkOptions ApplyEnvironment(CheckmkOptions current)
    {
        var mode = ParseMode(Environment.GetEnvironmentVariable("CHECKMK_MODE")) ?? current.Mode;
        var poll = ParsePositiveInt(Environment.GetEnvironmentVariable("CHECKMK_POLL_INTERVAL_SECONDS"))
                   ?? current.PollIntervalSeconds;

        return new CheckmkOptions
        {
            Mode = mode,
            BaseUrl = FirstNonEmpty(Environment.GetEnvironmentVariable("CHECKMK_BASE_URL"), current.BaseUrl),
            Site = FirstNonEmpty(Environment.GetEnvironmentVariable("CHECKMK_SITE"), current.Site),
            Username = FirstNonEmpty(Environment.GetEnvironmentVariable("CHECKMK_USERNAME"), current.Username),
            Secret = FirstNonEmpty(Environment.GetEnvironmentVariable("CHECKMK_SECRET"), current.Secret),
            PollIntervalSeconds = poll <= 0 ? CheckmkOptions.DefaultPollIntervalSeconds : poll
        };
    }

    internal static CheckmkOptions FromJson(string json)
    {
        using var document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("Checkmk", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<CheckmkOptions>(nested.GetRawText(), JsonOptions)
                   ?? new CheckmkOptions();
        }

        return JsonSerializer.Deserialize<CheckmkOptions>(json, JsonOptions) ?? new CheckmkOptions();
    }

    private static CheckmkOptions ReadFile(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return new CheckmkOptions();
        }

        return FromJson(File.ReadAllText(path));
    }

    internal static string? ResolveConfigPath()
    {
        foreach (var candidate in CandidatePaths())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidatePaths()
    {
        var explicitPath = Environment.GetEnvironmentVariable("CHECKMK_CONFIG");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            yield return explicitPath;
        }

        yield return Path.Combine(AppContext.BaseDirectory, "config", FileName);

        var directory = Directory.GetCurrentDirectory();
        for (var i = 0; i < 8 && !string.IsNullOrEmpty(directory); i++)
        {
            yield return Path.Combine(directory, "config", FileName);
            yield return Path.Combine(directory, FileName);
            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(appData))
        {
            yield return Path.Combine(appData, "CheckmkDesktopNotifier", FileName);
        }
    }

    private static string? FirstNonEmpty(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred.Trim();

    private static ClientMode? ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<ClientMode>(value.Trim(), ignoreCase: true, out var mode) ? mode : null;
    }

    private static int? ParsePositiveInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;
}
