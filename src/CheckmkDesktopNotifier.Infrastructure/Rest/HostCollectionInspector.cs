using System.Text.Json;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public sealed class HostCollectionInspection
{
    public int HostCount { get; init; }

    public bool StateAvailable { get; init; }

    public int? UpCount { get; init; }

    public int? DownCount { get; init; }

    public int? UnreachableCount { get; init; }

    public string IdentitySource { get; init; } = "none";

    public IReadOnlyList<string> PresentMonitoringFields { get; init; } = [];

    public IReadOnlyList<string> MissingMonitoringFields { get; init; } = [];

    public string NextRuntimeTest { get; init; } = string.Empty;
}

public static class HostCollectionInspector
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static HostCollectionInspection Inspect(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new CheckmkProtocolException("Host collection JSON could not be parsed.", ex);
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("value", out var value)
                || value.ValueKind != JsonValueKind.Array)
            {
                throw new CheckmkProtocolException("Host collection JSON did not contain a value array.");
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            var identityVotes = new Dictionary<string, int>(StringComparer.Ordinal);
            var hostCount = 0;
            var up = 0;
            var down = 0;
            var unreachable = 0;
            var sawState = false;

            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                hostCount++;
                var fields = ResolveHostFields(item);
                foreach (var key in fields.Keys)
                {
                    keys.Add(key);
                }

                var identity = DescribeIdentitySource(item, fields);
                identityVotes[identity] = identityVotes.GetValueOrDefault(identity) + 1;

                if (!TryGetField(fields, "state", out var stateElement))
                {
                    continue;
                }

                sawState = true;
                switch (JsonValueParser.ReadInt(stateElement))
                {
                    case 0:
                        up++;
                        break;
                    case 1:
                        down++;
                        break;
                    case 2:
                        unreachable++;
                        break;
                }
            }

            var present = CheckmkHostCollectionContract.ExpectedMonitoringFields
                .Where(keys.Contains)
                .ToArray();
            var missing = CheckmkHostCollectionContract.ExpectedMonitoringFields
                .Where(field => !keys.Contains(field))
                .ToArray();

            var identitySource = identityVotes.Count == 0
                ? "none"
                : identityVotes.OrderByDescending(pair => pair.Value).First().Key;

            return new HostCollectionInspection
            {
                HostCount = hostCount,
                StateAvailable = sawState,
                UpCount = sawState ? up : null,
                DownCount = sawState ? down : null,
                UnreachableCount = sawState ? unreachable : null,
                IdentitySource = identitySource,
                PresentMonitoringFields = present,
                MissingMonitoringFields = missing,
                NextRuntimeTest = NextTest(sawState, missing)
            };
        }
    }

    internal static Dictionary<string, JsonElement> ResolveHostFields(JsonElement item)
    {
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (item.TryGetProperty("extensions", out var extensions) && extensions.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in extensions.EnumerateObject())
            {
                fields[property.Name] = property.Value;
            }
        }

        return fields;
    }

    internal static string? ReadHostName(JsonElement item)
    {
        var fields = ResolveHostFields(item);
        if (TryGetField(fields, "name", out var name))
        {
            return JsonValueParser.ReadString(name);
        }

        if (item.TryGetProperty("id", out var id))
        {
            return JsonValueParser.ReadString(id);
        }

        if (item.TryGetProperty("title", out var title))
        {
            return JsonValueParser.ReadString(title);
        }

        return null;
    }

    private static bool TryGetField(IReadOnlyDictionary<string, JsonElement> fields, string name, out JsonElement value) =>
        fields.TryGetValue(name, out value);

    private static string DescribeIdentitySource(JsonElement item, IReadOnlyDictionary<string, JsonElement> fields)
    {
        if (fields.ContainsKey("name"))
        {
            return "extensions.name";
        }

        if (item.TryGetProperty("id", out var id)
            && id.ValueKind is JsonValueKind.String or JsonValueKind.Number)
        {
            return "id";
        }

        if (item.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
        {
            return "title";
        }

        return "none";
    }

    private static string NextTest(bool stateAvailable, IReadOnlyList<string> missing)
    {
        if (stateAvailable && !missing.Contains("state_type") && !missing.Contains("last_time_up"))
        {
            return "None for field discovery. Do not enable host monitoring in the app until this live host probe is accepted.";
        }

        return "Repeat GET /domain-types/host/collections/all with documented repeated columns= query parameters "
               + "(name, state, state_type, plugin_output, last_state_change, last_hard_state_change, "
               + "last_time_up, acknowledged, scheduled_downtime_depth). Do not send a JSON body. "
               + "Do not use host_config. Do not invent a host POST.";
    }
}
