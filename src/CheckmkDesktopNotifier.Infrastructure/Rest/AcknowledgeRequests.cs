using System.Text.Json.Serialization;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

internal sealed class AcknowledgeServiceRequest
{
    [JsonPropertyName("sticky")]
    public bool Sticky { get; init; } = true;

    [JsonPropertyName("persistent")]
    public bool Persistent { get; init; }

    [JsonPropertyName("notify")]
    public bool Notify { get; init; }

    [JsonPropertyName("comment")]
    public required string Comment { get; init; }

    [JsonPropertyName("acknowledge_type")]
    public string AcknowledgeType { get; init; } = "service";

    [JsonPropertyName("host_name")]
    public required string HostName { get; init; }

    [JsonPropertyName("service_description")]
    public required string ServiceDescription { get; init; }
}

internal sealed class AcknowledgeHostRequest
{
    [JsonPropertyName("sticky")]
    public bool Sticky { get; init; } = true;

    [JsonPropertyName("persistent")]
    public bool Persistent { get; init; }

    [JsonPropertyName("notify")]
    public bool Notify { get; init; }

    [JsonPropertyName("comment")]
    public required string Comment { get; init; }

    [JsonPropertyName("acknowledge_type")]
    public string AcknowledgeType { get; init; } = "host";

    [JsonPropertyName("host_name")]
    public required string HostName { get; init; }
}
