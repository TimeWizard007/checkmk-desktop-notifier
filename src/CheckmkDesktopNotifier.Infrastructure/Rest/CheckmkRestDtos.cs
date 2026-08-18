using System.Text.Json;
using System.Text.Json.Serialization;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

internal sealed class CheckmkServiceStatusRequest
{
    public static readonly CheckmkServiceStatusRequest Verified = CreateVerified();

    [JsonPropertyName("columns")]
    public required IReadOnlyList<string> Columns { get; init; }

    [JsonPropertyName("query")]
    public required CheckmkQueryNode Query { get; init; }

    private static CheckmkServiceStatusRequest CreateVerified() =>
        new()
        {
            Columns =
            [
                "host_name",
                "description",
                "state",
                "state_type",
                "plugin_output",
                "last_state_change",
                "last_hard_state_change",
                "last_time_ok",
                "acknowledged",
                "acknowledgement_type",
                "comments_with_extra_info",
                "scheduled_downtime_depth"
            ],
            Query = new CheckmkQueryNode
            {
                Op = "or",
                Expressions =
                [
                    new CheckmkQueryNode { Op = "=", Left = "state", Right = "1" },
                    new CheckmkQueryNode { Op = "=", Left = "state", Right = "2" },
                    new CheckmkQueryNode { Op = "=", Left = "state", Right = "3" }
                ]
            }
        };
}

internal sealed class CheckmkQueryNode
{
    [JsonPropertyName("op")]
    public required string Op { get; init; }

    [JsonPropertyName("left")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Left { get; init; }

    [JsonPropertyName("right")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Right { get; init; }

    [JsonPropertyName("expr")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CheckmkQueryNode>? Expressions { get; init; }
}

internal sealed class CheckmkCollectionResponse
{
    [JsonPropertyName("value")]
    public List<CheckmkCollectionItemDto>? Value { get; set; }
}

internal sealed class CheckmkCollectionItemDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("domainType")]
    public string? DomainType { get; set; }

    [JsonPropertyName("extensions")]
    public JsonElement Extensions { get; set; }
}

internal static class CheckmkHostCollectionContract
{
    public const string HostCollectionPath = "domain-types/host/collections/all";

    public static readonly string[] ExpectedMonitoringFields =
    [
        "name",
        "state",
        "state_type",
        "plugin_output",
        "last_state_change",
        "last_hard_state_change",
        "last_time_up",
        "last_time_down",
        "last_time_unreachable",
        "acknowledged",
        "acknowledgement_type",
        "comments_with_extra_info",
        "scheduled_downtime_depth",
        "num_services_hard_crit",
        "num_services_hard_warn",
        "num_services_hard_unknown"
    ];

    public static readonly string[] DocumentedColumnsQueryParameters = ExpectedMonitoringFields;

    public static string CreateDocumentedColumnsRelativeUri()
    {
        var query = string.Join(
            "&",
            DocumentedColumnsQueryParameters.Select(column => "columns=" + Uri.EscapeDataString(column)));
        return HostCollectionPath + "?" + query;
    }
}
