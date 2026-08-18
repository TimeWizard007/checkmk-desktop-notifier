using System.Text.Json;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public static class ServiceProblemMapper
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
            throw new CheckmkProtocolException("Service collection JSON could not be parsed.", ex);
        }

        using (document)
        {
            if (!CheckmkJson.TryGetProperty(document.RootElement, "value", out var value)
                || value.ValueKind != JsonValueKind.Array)
            {
                throw new CheckmkProtocolException("Service collection JSON did not contain a value array.");
            }

            var problems = new List<MonitoredProblem>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!CheckmkJson.TryGetProperty(item, "extensions", out var extensions)
                    || extensions.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (TryMapService(extensions, siteId, out var problem) && problem is not null)
                {
                    problems.Add(problem);
                }
            }

            return problems;
        }
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

        var ack = CheckmkCommentMapper.ReadAcknowledgement(extensions);
        problem = new MonitoredProblem
        {
            Id = MonitoredObjectId.Service(siteId, hostName, description),
            Severity = severity.Value,
            StateType = mappedStateType.Value,
            PluginOutput = Truncate(JsonValueParser.ReadString(extensions, "plugin_output")),
            LastStateChange = UnixTimeMapper.FromUnixSeconds(JsonValueParser.ReadInt64(extensions, "last_state_change")),
            LastHardStateChange = UnixTimeMapper.FromUnixSeconds(JsonValueParser.ReadInt64(extensions, "last_hard_state_change")),
            LastTimeOk = UnixTimeMapper.FromUnixSeconds(JsonValueParser.ReadInt64(extensions, "last_time_ok")),
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
