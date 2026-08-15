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
    [JsonPropertyName("extensions")]
    public JsonElement Extensions { get; set; }
}
