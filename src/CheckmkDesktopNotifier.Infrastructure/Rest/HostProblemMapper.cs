using System.Text.Json;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public static class HostProblemMapper
{
    public const int MaxPluginOutputLength = 512;

    public static IReadOnlyList<MonitoredProblem> MapCollection(string json, SiteId siteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, CheckmkJson.DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new CheckmkProtocolException("Host collection JSON could not be parsed.", ex);
        }

        using (document)
        {
            if (!CheckmkJson.TryGetProperty(document.RootElement, "value", out var value)
                || value.ValueKind != JsonValueKind.Array)
            {
                throw new CheckmkProtocolException("Host collection JSON did not contain a value array.");
            }

            var problems = new List<MonitoredProblem>();
            foreach (var item in value.EnumerateArray())
            {
                if (TryMapHost(item, siteId, out var problem) && problem is not null)
                {
                    problems.Add(problem);
                }
            }

            return problems;
        }
    }

    public static IReadOnlyList<MonitoredProblem> MapHardProblems(string json, SiteId siteId) =>
        MapCollection(json, siteId).Where(problem => problem.StateType == StateType.Hard).ToArray();

    internal static bool TryMapHost(JsonElement item, SiteId siteId, out MonitoredProblem? problem)
    {
        problem = null;
        JsonElement extensions = default;
        if (item.ValueKind == JsonValueKind.Object)
        {
            CheckmkJson.TryGetProperty(item, "extensions", out extensions);
        }

        var hostName = extensions.ValueKind == JsonValueKind.Object
            ? JsonValueParser.ReadString(extensions, "name")
            : null;
        if (item.ValueKind == JsonValueKind.Object)
        {
            hostName ??= JsonValueParser.ReadString(item, "id");
            hostName ??= JsonValueParser.ReadString(item, "title");
        }

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

        var ack = CheckmkCommentMapper.ReadAcknowledgement(extensions);
        problem = new MonitoredProblem
        {
            Id = MonitoredObjectId.Host(siteId, hostName),
            Severity = severity.Value,
            StateType = mappedStateType.Value,
            PluginOutput = Truncate(JsonValueParser.ReadString(extensions, "plugin_output")),
            LastStateChange = UnixTimeMapper.FromUnixSeconds(JsonValueParser.ReadInt64(extensions, "last_state_change")),
            LastHardStateChange = UnixTimeMapper.FromUnixSeconds(JsonValueParser.ReadInt64(extensions, "last_hard_state_change")),
            LastTimeUp = UnixTimeMapper.FromUnixSeconds(JsonValueParser.ReadInt64(extensions, "last_time_up")),
            IsAcknowledgedInCheckmk = ack.IsAcknowledged,
            AcknowledgementType = ack.AcknowledgementType,
            TakenByDisplayName = ack.TakenByDisplayName,
            IsTakenByNotifier = ack.IsTakenByNotifier,
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
