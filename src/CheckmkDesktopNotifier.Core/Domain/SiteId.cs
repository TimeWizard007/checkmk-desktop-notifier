namespace CheckmkDesktopNotifier.Core.Domain;

public readonly record struct SiteId
{
    public string Value { get; }

    public SiteId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Site id must not be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
