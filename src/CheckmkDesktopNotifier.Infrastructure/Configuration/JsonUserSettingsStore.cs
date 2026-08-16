using System.Text.Json;
using System.Text.Json.Serialization;

namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public interface IUserSettingsStore
{
    bool Exists { get; }

    UserSettings? Load();

    void Save(UserSettings settings);

    void Delete();
}

public sealed class JsonUserSettingsStore : IUserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _filePath;

    public JsonUserSettingsStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Settings path must not be empty.", nameof(filePath));
        }

        _filePath = filePath;
    }

    public bool Exists => File.Exists(_filePath);

    public UserSettings? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var json = File.ReadAllText(_filePath);
        using (var document = JsonDocument.Parse(json))
        {
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Name.Equals("Secret", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "Settings file must not contain a secret or Authorization header.");
                    }
                }
            }
        }

        return JsonSerializer.Deserialize<UserSettings>(json, JsonOptions)
               ?? throw new InvalidOperationException("Settings file is empty.");
    }

    public void Save(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var dto = new UserSettings
        {
            BaseUrl = settings.BaseUrl?.Trim().TrimEnd('/'),
            Site = settings.Site?.Trim(),
            Username = settings.Username?.Trim(),
            PollIntervalSeconds = settings.PollIntervalSeconds
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    public void Delete()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
