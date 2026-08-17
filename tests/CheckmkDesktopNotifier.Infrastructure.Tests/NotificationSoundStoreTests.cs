using System.Buffers.Binary;
using CheckmkDesktopNotifier.Core.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class NotificationSoundStoreTests
{
    [Fact]
    public void Valid_wav_is_copied_into_app_owned_storage()
    {
        using var folder = new TempFolder();
        var source = Path.Combine(folder.Path, "my-alert.wav");
        File.WriteAllBytes(source, CreateTone());
        var store = new NotificationSoundStore(new AppStoragePaths(folder.Path));

        var result = store.ImportFrom(source);

        Assert.True(result.Succeeded);
        Assert.Equal("my-alert.wav", result.FileName);
        Assert.True(File.Exists(store.CustomSoundPath));
        Assert.NotEqual(source, store.CustomSoundPath);
        Assert.Contains("assets", store.CustomSoundPath, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(store.TryReadCustomBytes());
    }

    [Fact]
    public void Original_source_deletion_does_not_break_imported_sound()
    {
        using var folder = new TempFolder();
        var source = Path.Combine(folder.Path, "source.wav");
        File.WriteAllBytes(source, CreateTone(20000));
        var store = new NotificationSoundStore(new AppStoragePaths(folder.Path));
        Assert.True(store.ImportFrom(source).Succeeded);
        File.Delete(source);

        var custom = store.TryReadCustomBytes();
        Assert.NotNull(custom);
        var mixed = NotificationSoundMixer.Mix(CreateTone(10000), custom, NotificationSoundSource.Custom, 100);
        Assert.True(PcmWavParser.TryParse(mixed, out var wav, out _));
        Assert.Equal(20000, BinaryPrimitives.ReadInt16LittleEndian(wav.Pcm.AsSpan(0, 2)));
    }

    [Fact]
    public void Invalid_wav_is_rejected_and_does_not_replace_working_sound()
    {
        using var folder = new TempFolder();
        var store = new NotificationSoundStore(new AppStoragePaths(folder.Path));
        var good = Path.Combine(folder.Path, "good.wav");
        File.WriteAllBytes(good, CreateTone());
        Assert.True(store.ImportFrom(good).Succeeded);
        var before = File.ReadAllBytes(store.CustomSoundPath);

        var bad = Path.Combine(folder.Path, "bad.wav");
        File.WriteAllBytes(bad, "RIFF????WAVE"u8.ToArray());
        var result = store.ImportFrom(bad);

        Assert.False(result.Succeeded);
        Assert.Equal(NotificationSoundImportStatus.InvalidFormat, result.Status);
        Assert.Equal(before, File.ReadAllBytes(store.CustomSoundPath));
    }

    [Fact]
    public void Missing_imported_wav_falls_back_safely()
    {
        using var folder = new TempFolder();
        var store = new NotificationSoundStore(new AppStoragePaths(folder.Path));
        Assert.Null(store.TryReadCustomBytes());
        var bundled = CreateTone(1234);
        var mixed = NotificationSoundMixer.Mix(bundled, store.TryReadCustomBytes() ?? [], NotificationSoundSource.Custom, 100);
        Assert.True(PcmWavParser.TryParse(mixed, out var wav, out _));
        Assert.Equal(1234, BinaryPrimitives.ReadInt16LittleEndian(wav.Pcm.AsSpan(0, 2)));
    }

    [Fact]
    public void Restore_default_deletes_custom_file()
    {
        using var folder = new TempFolder();
        var source = Path.Combine(folder.Path, "custom.wav");
        File.WriteAllBytes(source, CreateTone());
        var store = new NotificationSoundStore(new AppStoragePaths(folder.Path));
        Assert.True(store.ImportFrom(source).Succeeded);
        store.DeleteCustomIfPresent();
        Assert.False(File.Exists(store.CustomSoundPath));
        Assert.Null(store.TryReadCustomBytes());
    }

    [Fact]
    public void Volume_and_source_and_mute_persist()
    {
        using var folder = new TempFolder();
        var path = Path.Combine(folder.Path, "preferences.json");
        var first = new JsonUserPreferencesStore(path);
        Assert.Equal(PcmWavLimits.DefaultVolumePercent, first.VolumePercent);
        Assert.Equal(NotificationSoundSource.Default, first.SoundSource);
        first.SetMuteSound(true);
        first.SetVolumePercent(40);
        first.SetSoundSource(NotificationSoundSource.Custom);
        first.SetCustomSoundFileName(@"C:\temp\alert.wav");

        var second = new JsonUserPreferencesStore(path);
        Assert.True(second.MuteSound);
        Assert.Equal(40, second.VolumePercent);
        Assert.Equal(NotificationSoundSource.Custom, second.SoundSource);
        Assert.Equal("alert.wav", second.CustomSoundFileName);
        Assert.DoesNotContain(@"C:\temp", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_failure_does_not_throw()
    {
        using var folder = new TempFolder();
        var store = new NotificationSoundStore(new AppStoragePaths(folder.Path));
        var missing = Path.Combine(folder.Path, "missing.wav");
        var exception = Record.Exception(() => store.ImportFrom(missing));
        Assert.Null(exception);
        Assert.Equal(NotificationSoundImportStatus.IoError, store.ImportFrom(missing).Status);
    }

    private static byte[] CreateTone(short amplitude = 10000)
    {
        var pcm = new byte[440];
        for (var i = 0; i < 220; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2, 2), amplitude);
        }

        return PcmWavWriter.Write(new PcmWav
        {
            SampleRate = 22050,
            Channels = 1,
            BitsPerSample = 16,
            Pcm = pcm
        });
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "checkmk-sound-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

