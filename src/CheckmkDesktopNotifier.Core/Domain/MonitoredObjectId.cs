namespace CheckmkDesktopNotifier.Core.Domain;

public sealed record MonitoredObjectId
{
    public SiteId SiteId { get; }
    public ObjectKind Kind { get; }
    public string HostName { get; }
    public string? ServiceDescription { get; }

    public MonitoredObjectId(
        SiteId siteId,
        ObjectKind kind,
        string hostName,
        string? serviceDescription = null)
    {
        if (string.IsNullOrWhiteSpace(hostName))
        {
            throw new ArgumentException("Host name must not be empty.", nameof(hostName));
        }

        SiteId = siteId;
        Kind = kind;
        HostName = hostName.Trim();

        switch (kind)
        {
            case ObjectKind.Host:
                if (!string.IsNullOrWhiteSpace(serviceDescription))
                {
                    throw new ArgumentException(
                        "Host identity must not include a service description.",
                        nameof(serviceDescription));
                }

                ServiceDescription = null;
                break;

            case ObjectKind.Service:
                if (string.IsNullOrWhiteSpace(serviceDescription))
                {
                    throw new ArgumentException(
                        "Service identity requires a service description.",
                        nameof(serviceDescription));
                }

                ServiceDescription = serviceDescription.Trim();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown object kind.");
        }
    }

    public static MonitoredObjectId Host(SiteId siteId, string hostName) =>
        new(siteId, ObjectKind.Host, hostName);

    public static MonitoredObjectId Service(SiteId siteId, string hostName, string serviceDescription) =>
        new(siteId, ObjectKind.Service, hostName, serviceDescription);

    public override string ToString() =>
        Kind == ObjectKind.Host
            ? $"{SiteId.Value}/{HostName}"
            : $"{SiteId.Value}/{HostName}/{ServiceDescription}";
}
