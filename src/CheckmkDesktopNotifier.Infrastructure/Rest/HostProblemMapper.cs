using System.Text.Json;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public static class HostProblemMapper
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
                       ?? throw new CheckmkProtocolException("Host collection JSON was empty.");
        }
        catch (JsonException ex)
        {
            throw new CheckmkProtocolException("Host collection JSON could not be parsed.", ex);
        }

        if (response.Value is null)
        {
            throw new CheckmkProtocolException("Host collection JSON did not contain a value array.");
        }

        var problems = new List<MonitoredProblem>();
        foreach (var item in response.Value)
        {
            if (TryMapHost(item, siteId, out var problem) && problem is not null)
            {
                problems.Add(problem);
            }
        }

        return problems;
    }

    public static IReadOnlyList<MonitoredProblem> MapHardProblems(string json, SiteId siteId) =>
        MapCollection(json, siteId).Where(problem => problem.StateType == StateType.Hard).ToArray();

    internal static bool TryMapHost(CheckmkCollectionItemDto item, SiteId siteId, out MonitoredProblem? problem)
    {
        problem = null;
        var extensions = item.Extensions.ValueKind == JsonValueKind.Object
            ? item.Extensions
            : default;

        var hostName = extensions.ValueKind == JsonValueKind.Object
            ? JsonValueParser.ReadString(extensions, "name")
            : null;
        hostName ??= item.Id;
        hostName ??= item.Title;

        if (string.IsNullOrWhiteSpace(hostName) || extensions.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var state = JsonValueParser.ReadInt(extensions, "state");
        var stateType = JsonValueParser.ReadInt(extensions, "state_type");
        if (state is null || stateType is null)
        {
            return false;
        }

        var severity = JsonValueParser.MapHostSeverity(state.Value);
        var mappedStateType = JsonValueParser.MapStateType(stateType.Value);
        if (severity is null || mappedStateType is null)
        {
            return false;
        }

        problem = new MonitoredProblem
        {
            Id = MonitoredObjectId.Host(siteId, hostName),
            Severity = severity.Value,
            StateType = mappedStateType.Value,
            PluginOutput = Truncate(JsonValueParser.ReadString(extensions, "plugin_output")),
            LastStateChange = UnixTimeMapper.FromUnixSeconds(JsonValueParser.ReadInt64(extensions, "last_state_change")),
            LastHardStateChange = UnixTimeMapper.FromUnixSeconds(JsonValueParser.ReadInt64(extensions, "last_hard_state_change")),
            LastTimeUp = UnixTimeMapper.FromUnixSeconds(JsonValueParser.ReadInt64(extensions, "last_time_up")),
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
