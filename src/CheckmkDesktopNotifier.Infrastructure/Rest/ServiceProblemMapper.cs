using System.Text.Json;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public static class ServiceProblemMapper
{
    public const int MaxPluginOutputLength = 512;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<MonitoredProblem> MapCollection(string json, SiteId siteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        CheckmkCollectionResponse response;
        try
        {
            response = JsonSerializer.Deserialize<CheckmkCollectionResponse>(json, JsonOptions)
                       ?? throw new CheckmkProtocolException("Service collection JSON was empty.");
        }
        catch (JsonException ex)
        {
            throw new CheckmkProtocolException("Service collection JSON could not be parsed.", ex);
        }

        if (response.Value is null)
        {
            throw new CheckmkProtocolException("Service collection JSON did not contain a value array.");
        }

        var problems = new List<MonitoredProblem>(response.Value.Count);
        foreach (var item in response.Value)
        {
            if (item.Extensions.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (TryMapService(item.Extensions, siteId, out var problem) && problem is not null)
            {
                problems.Add(problem);
            }
        }

        return problems;
    }

    internal static bool TryMapService(JsonElement extensions, SiteId siteId, out MonitoredProblem? problem)
    {
        problem = null;
        var hostName = JsonValueParser.ReadString(extensions, "host_name");
        var description = JsonValueParser.ReadString(extensions, "description");
        var state = JsonValueParser.ReadInt(extensions, "state");
        var stateType = JsonValueParser.ReadInt(extensions, "state_type");

        if (string.IsNullOrWhiteSpace(hostName)
            || string.IsNullOrWhiteSpace(description)
            || state is null
            || stateType is null)
        {
            return false;
        }

        var severity = JsonValueParser.MapServiceSeverity(state.Value);
        var mappedStateType = JsonValueParser.MapStateType(stateType.Value);
        if (severity is null || mappedStateType is null)
        {
            return false;
        }

        problem = new MonitoredProblem
        {
            Id = MonitoredObjectId.Service(siteId, hostName, description),
            Severity = severity.Value,
            StateType = mappedStateType.Value,
            PluginOutput = Truncate(JsonValueParser.ReadString(extensions, "plugin_output")),
            LastStateChange = UnixTimeMapper.FromUnixSeconds(JsonValueParser.ReadInt64(extensions, "last_state_change")),
            LastHardStateChange = UnixTimeMapper.FromUnixSeconds(JsonValueParser.ReadInt64(extensions, "last_hard_state_change")),
            LastTimeOk = UnixTimeMapper.FromUnixSeconds(JsonValueParser.ReadInt64(extensions, "last_time_ok")),
            IsAcknowledgedInCheckmk = (JsonValueParser.ReadInt(extensions, "acknowledged") ?? 0) != 0,
            ScheduledDowntimeDepth = JsonValueParser.ReadInt(extensions, "scheduled_downtime_depth") ?? 0
        };

        return true;
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MaxPluginOutputLength
            ? trimmed
            : trimmed[..MaxPluginOutputLength];
    }
}

public sealed class CheckmkProtocolException : Exception
{
    public CheckmkProtocolException(string message)
        : base(message)
    {
    }

    public CheckmkProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
