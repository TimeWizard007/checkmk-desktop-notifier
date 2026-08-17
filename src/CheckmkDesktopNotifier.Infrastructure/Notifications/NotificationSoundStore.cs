using CheckmkDesktopNotifier.Core.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Infrastructure.Notifications;

public enum NotificationSoundImportStatus
{
    Success = 0,
    InvalidFormat,
    IoError
}

public sealed class NotificationSoundImportResult
{
    public required NotificationSoundImportStatus Status { get; init; }

    public string? FileName { get; init; }

    public bool Succeeded => Status == NotificationSoundImportStatus.Success;
}

/// <summary>
/// Copies a validated WAV into app-owned LocalAppData. Playback never depends on the original path.
/// </summary>
public sealed class NotificationSoundStore
{
    private readonly AppStoragePaths _paths;

    public NotificationSoundStore(AppStoragePaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public string CustomSoundPath => _paths.CustomNotificationSoundPath;

    public NotificationSoundImportResult ImportFrom(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return new NotificationSoundImportResult { Status = NotificationSoundImportStatus.InvalidFormat };
        }

        try
        {
            var bytes = File.ReadAllBytes(sourcePath);
            if (!PcmWavParser.TryParse(bytes, out _, out _))
            {
                return new NotificationSoundImportResult { Status = NotificationSoundImportStatus.InvalidFormat };
            }

            var destination = CustomSoundPath;
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = destination + ".tmp";
            File.WriteAllBytes(tempPath, bytes);
            File.Move(tempPath, destination, overwrite: true);
            return new NotificationSoundImportResult
            {
                Status = NotificationSoundImportStatus.Success,
                FileName = Path.GetFileName(sourcePath)
            };
        }
        catch (Exception)
        {
            return new NotificationSoundImportResult { Status = NotificationSoundImportStatus.IoError };
        }
    }

    public byte[]? TryReadCustomBytes()
    {
        try
        {
            var path = CustomSoundPath;
            if (!File.Exists(path))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(path);
            return PcmWavParser.TryParse(bytes, out _, out _) ? bytes : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void DeleteCustomIfPresent()
    {
        try
        {
            if (File.Exists(CustomSoundPath))
            {
                File.Delete(CustomSoundPath);
            }
        }
        catch (Exception)
        {
        }
    }
}
