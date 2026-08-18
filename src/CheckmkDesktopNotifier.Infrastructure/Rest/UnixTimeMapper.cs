using System.Text.Json;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public static class UnixTimeMapper
{
    public static DateTimeOffset? FromUnixSeconds(long? seconds)
    {
        if (seconds is null or <= 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}

internal static class JsonValueParser
{
    public static string? ReadString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };

    public static int? ReadInt(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var i) => i,
            JsonValueKind.Number when value.TryGetInt64(out var l) => (int)l,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => null
        };

    public static long? ReadInt64(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var l) => l,
            JsonValueKind.Number when value.TryGetDouble(out var d) => (long)d,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };

    public static string? ReadString(System.Text.Json.JsonElement element, string name)
    {
        if (!CheckmkJson.TryGetProperty(element, name, out var value))
        {
            return null;
        }

        return ReadString(value);
    }

    public static int? ReadInt(System.Text.Json.JsonElement element, string name)
    {
        if (!CheckmkJson.TryGetProperty(element, name, out var value))
        {
            return null;
        }

        return ReadInt(value);
    }

    public static long? ReadInt64(System.Text.Json.JsonElement element, string name)
    {
        if (!CheckmkJson.TryGetProperty(element, name, out var value))
        {
            return null;
        }

        return ReadInt64(value);
    }

    public static Severity? MapServiceSeverity(int state) =>
        state switch
        {
            1 => Severity.Warning,
            2 => Severity.Critical,
            3 => Severity.Unknown,
            _ => null
        };

    public static StateType? MapStateType(int stateType) =>
        stateType switch
        {
            0 => StateType.Soft,
            1 => StateType.Hard,
            _ => null
        };

    public static Severity? MapHostSeverity(int state) =>
        state switch
        {
            1 => Severity.Critical,
            2 => Severity.Unknown,
            _ => null
        };
}
