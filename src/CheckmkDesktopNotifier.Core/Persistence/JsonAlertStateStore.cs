using System.Text.Json;
using System.Text.Json.Serialization;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Persistence;

public sealed class JsonAlertStateStore : IAlertStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;

    public JsonAlertStateStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("State file path must not be empty.", nameof(filePath));
        }

        _filePath = filePath;
    }

    public AlertStateDocument? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var json = File.ReadAllText(_filePath);
        var dto = JsonSerializer.Deserialize<AlertStateFileDto>(json, SerializerOptions)
                  ?? throw new InvalidOperationException($"Alert state file '{_filePath}' is empty.");

        if (dto.SchemaVersion > AlertStateDocument.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Alert state schema {dto.SchemaVersion} is newer than supported version {AlertStateDocument.CurrentSchemaVersion}.");
        }

        if (dto.SchemaVersion < 1)
        {
            throw new InvalidOperationException($"Alert state schema {dto.SchemaVersion} is not supported.");
        }

        var incidents = (dto.Incidents ?? []).Select(MapIncident).ToArray();
        return new AlertStateDocument
        {
            SchemaVersion = dto.SchemaVersion,
            LastSuccessfulPollUtc = dto.LastSuccessfulPollUtc,
            Incidents = incidents
        };
    }

    public void Save(AlertStateDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var dto = new AlertStateFileDto
        {
            SchemaVersion = AlertStateDocument.CurrentSchemaVersion,
            LastSuccessfulPollUtc = document.LastSuccessfulPollUtc,
            Incidents = document.Incidents.Select(MapIncident).ToList()
        };

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(dto, SerializerOptions);
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static OpenIncident MapIncident(PersistedIncidentDto dto)
    {
        var siteId = new SiteId(dto.SiteId);
        var objectId = dto.Kind == ObjectKind.Host
            ? MonitoredObjectId.Host(siteId, dto.HostName)
            : MonitoredObjectId.Service(siteId, dto.HostName, dto.ServiceDescription ?? string.Empty);

        return new OpenIncident
        {
            ObjectId = objectId,
            Severity = dto.Severity,
            IsSeen = dto.IsSeen,
            OpenedAtUtc = dto.OpenedAtUtc,
            LastObservedAtUtc = dto.LastObservedAtUtc,
            BoundRecurrenceMarker = dto.BoundRecurrenceMarker,
            LastSummary = dto.LastSummary,
            IsAcknowledgedInCheckmk = dto.IsAcknowledgedInCheckmk,
            ScheduledDowntimeDepth = dto.ScheduledDowntimeDepth
        };
    }

    private static PersistedIncidentDto MapIncident(OpenIncident incident) =>
        new()
        {
            SiteId = incident.ObjectId.SiteId.Value,
            Kind = incident.ObjectId.Kind,
            HostName = incident.ObjectId.HostName,
            ServiceDescription = incident.ObjectId.ServiceDescription,
            Severity = incident.Severity,
            IsSeen = incident.IsSeen,
            OpenedAtUtc = incident.OpenedAtUtc,
            LastObservedAtUtc = incident.LastObservedAtUtc,
            BoundRecurrenceMarker = incident.BoundRecurrenceMarker,
            LastSummary = incident.LastSummary,
            IsAcknowledgedInCheckmk = incident.IsAcknowledgedInCheckmk,
            ScheduledDowntimeDepth = incident.ScheduledDowntimeDepth
        };

    private sealed class AlertStateFileDto
    {
        public int SchemaVersion { get; set; }
        public DateTimeOffset? LastSuccessfulPollUtc { get; set; }
        public List<PersistedIncidentDto>? Incidents { get; set; }
    }

    private sealed class PersistedIncidentDto
    {
        public string SiteId { get; set; } = string.Empty;
        public ObjectKind Kind { get; set; }
        public string HostName { get; set; } = string.Empty;
        public string? ServiceDescription { get; set; }
        public Severity Severity { get; set; }
        public bool IsSeen { get; set; }
        public DateTimeOffset OpenedAtUtc { get; set; }
        public DateTimeOffset LastObservedAtUtc { get; set; }
        public DateTimeOffset? BoundRecurrenceMarker { get; set; }
        public string? LastSummary { get; set; }
        public bool IsAcknowledgedInCheckmk { get; set; }
        public int ScheduledDowntimeDepth { get; set; }
    }
}
