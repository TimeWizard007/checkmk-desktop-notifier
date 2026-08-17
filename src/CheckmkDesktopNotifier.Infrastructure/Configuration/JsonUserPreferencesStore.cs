using System.Text.Json;
using System.Text.Json.Serialization;
using CheckmkDesktopNotifier.Core.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Notifications;

namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public sealed class UserPreferencesDocument
{
    public bool MuteSound { get; set; }

    public int? VolumePercent { get; set; }

    public string? SoundSource { get; set; }

    public string? CustomSoundFileName { get; set; }
}

public sealed class JsonUserPreferencesStore : IUserPreferences
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _filePath;
    private readonly object _gate = new();
    private bool _muteSound;
    private int _volumePercent = PcmWavLimits.DefaultVolumePercent;
    private NotificationSoundSource _soundSource = NotificationSoundSource.Default;
    private string? _customSoundFileName;

    public JsonUserPreferencesStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Preferences path must not be empty.", nameof(filePath));
        }

        _filePath = filePath;
        LoadUnlocked();
    }

    public bool MuteSound
    {
        get
        {
            lock (_gate)
            {
                return _muteSound;
            }
        }
    }

    public int VolumePercent
    {
        get
        {
            lock (_gate)
            {
                return _volumePercent;
            }
        }
    }

    public NotificationSoundSource SoundSource
    {
        get
        {
            lock (_gate)
            {
                return _soundSource;
            }
        }
    }

    public string? CustomSoundFileName
    {
        get
        {
            lock (_gate)
            {
                return _customSoundFileName;
            }
        }
    }

    public event EventHandler? Changed;

    public void SetMuteSound(bool mute) => Set(() =>
    {
        if (_muteSound == mute)
        {
            return false;
        }

        _muteSound = mute;
        return true;
    });

    public void SetVolumePercent(int volumePercent) => Set(() =>
    {
        var clamped = PcmWavVolume.ClampPercent(volumePercent);
        if (_volumePercent == clamped)
        {
            return false;
        }

        _volumePercent = clamped;
        return true;
    });

    public void SetSoundSource(NotificationSoundSource source) => Set(() =>
    {
        if (_soundSource == source)
        {
            return false;
        }

        _soundSource = source;
        return true;
    });

    public void SetCustomSoundFileName(string? fileName) => Set(() =>
    {
        var normalized = UserPreferenceFileNames.Display(fileName);
        if (string.Equals(_customSoundFileName, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        _customSoundFileName = normalized;
        return true;
    });

    private void Set(Func<bool> mutate)
    {
        lock (_gate)
        {
            if (!mutate())
            {
                return;
            }

            try
            {
                SaveUnlocked();
            }
            catch (Exception)
            {
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void LoadUnlocked()
    {
        _muteSound = false;
        _volumePercent = PcmWavLimits.DefaultVolumePercent;
        _soundSource = NotificationSoundSource.Default;
        _customSoundFileName = null;
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            var json = File.ReadAllText(_filePath);
            var document = JsonSerializer.Deserialize<UserPreferencesDocument>(json, JsonOptions);
            if (document is null)
            {
                return;
            }

            _muteSound = document.MuteSound;
            _volumePercent = document.VolumePercent is int volume
                ? PcmWavVolume.ClampPercent(volume)
                : PcmWavLimits.DefaultVolumePercent;
            _soundSource = string.Equals(document.SoundSource, nameof(NotificationSoundSource.Custom), StringComparison.OrdinalIgnoreCase)
                ? NotificationSoundSource.Custom
                : NotificationSoundSource.Default;
            _customSoundFileName = UserPreferenceFileNames.Display(document.CustomSoundFileName);
        }
        catch (Exception)
        {
        }
    }

    private void SaveUnlocked()
    {
        var json = JsonSerializer.Serialize(
            new UserPreferencesDocument
            {
                MuteSound = _muteSound,
                VolumePercent = _volumePercent,
                SoundSource = _soundSource.ToString(),
                CustomSoundFileName = _customSoundFileName
            },
            JsonOptions);
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }
}

public sealed class InMemoryUserPreferences : IUserPreferences
{
    public bool MuteSound { get; private set; }

    public int VolumePercent { get; private set; } = PcmWavLimits.DefaultVolumePercent;

    public NotificationSoundSource SoundSource { get; private set; } = NotificationSoundSource.Default;

    public string? CustomSoundFileName { get; private set; }

    public event EventHandler? Changed;

    public void SetMuteSound(bool mute)
    {
        if (MuteSound == mute)
        {
            return;
        }

        MuteSound = mute;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetVolumePercent(int volumePercent)
    {
        var clamped = PcmWavVolume.ClampPercent(volumePercent);
        if (VolumePercent == clamped)
        {
            return;
        }

        VolumePercent = clamped;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetSoundSource(NotificationSoundSource source)
    {
        if (SoundSource == source)
        {
            return;
        }

        SoundSource = source;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetCustomSoundFileName(string? fileName)
    {
        var normalized = UserPreferenceFileNames.Display(fileName);
        if (string.Equals(CustomSoundFileName, normalized, StringComparison.Ordinal))
        {
            return;
        }

        CustomSoundFileName = normalized;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

internal static class UserPreferenceFileNames
{
    public static string? Display(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var trimmed = fileName.Trim();
        var slash = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
        return slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
    }
}
