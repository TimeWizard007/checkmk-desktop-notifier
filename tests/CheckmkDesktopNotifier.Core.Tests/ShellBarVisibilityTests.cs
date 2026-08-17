using System.Buffers.Binary;
using CheckmkDesktopNotifier.Core;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class ShellBarVisibilityTests
{
    [Fact]
    public void Hide_to_tray_hides_without_creating_a_new_surface()
    {
        var session = new RecordingBarSession();
        session.HideToTray();

        Assert.False(session.Visibility.IsVisible);
        Assert.Equal(1, session.HideExistingCalls);
        Assert.Equal(0, session.ShowExistingCalls);
        Assert.Equal(1, session.InstanceCount);
    }

    [Fact]
    public void Restore_open_shows_the_existing_surface()
    {
        var session = new RecordingBarSession();
        session.HideToTray();
        session.Restore();

        Assert.True(session.Visibility.IsVisible);
        Assert.Equal(1, session.ShowExistingCalls);
        Assert.Equal(1, session.InstanceCount);
    }

    [Fact]
    public void Repeated_hide_and_restore_reuse_the_same_window_instance()
    {
        var session = new RecordingBarSession();
        session.HideToTray();
        session.Restore();
        session.HideToTray();
        session.Restore();

        Assert.True(session.Visibility.IsVisible);
        Assert.Equal(2, session.HideExistingCalls);
        Assert.Equal(2, session.ShowExistingCalls);
        Assert.Equal(1, session.InstanceCount);
    }

    [Fact]
    public void Tray_left_click_toggles_visible_to_hidden_and_hidden_to_visible()
    {
        var session = new RecordingBarSession();
        Assert.True(session.Visibility.IsVisible);

        session.ToggleFromTrayClick();
        Assert.False(session.Visibility.IsVisible);

        session.ToggleFromTrayClick();
        Assert.True(session.Visibility.IsVisible);
        Assert.Equal(1, session.HideExistingCalls);
        Assert.Equal(1, session.ShowExistingCalls);
        Assert.Equal(1, session.InstanceCount);
    }

    [Fact]
    public void Tray_open_always_restores_even_when_already_visible()
    {
        var session = new RecordingBarSession();
        session.Restore();
        session.Restore();

        Assert.True(session.Visibility.IsVisible);
        Assert.Equal(2, session.ShowExistingCalls);
        Assert.Equal(0, session.HideExistingCalls);
    }

    [Fact]
    public void Gear_hide_and_tray_open_share_the_same_visibility_state()
    {
        var session = new RecordingBarSession();
        session.HideToTray();
        session.Restore();

        Assert.Same(session.Visibility, session.Visibility);
        Assert.True(session.Visibility.IsVisible);
        Assert.Equal(1, session.InstanceCount);
    }

    private sealed class RecordingBarSession
    {
        public ShellBarVisibility Visibility { get; } = new();

        public int InstanceCount { get; } = 1;

        public int HideExistingCalls { get; private set; }

        public int ShowExistingCalls { get; private set; }

        public void HideToTray()
        {
            Visibility.HideToTray();
            HideExistingCalls++;
        }

        public void Restore()
        {
            Visibility.Restore();
            ShowExistingCalls++;
        }

        public void ToggleFromTrayClick()
        {
            var wasVisible = Visibility.IsVisible;
            Visibility.ToggleFromTrayClick();
            if (wasVisible)
            {
                HideExistingCalls++;
            }
            else
            {
                ShowExistingCalls++;
            }
        }
    }
}

public sealed class AlertSoundAssetTests
{
    [Fact]
    public void Bundled_notifier_wav_is_a_short_pcm_mono_resource()
    {
        var path = Path.Combine(FindRepoRoot(), AlertSoundAsset.RelativePath);
        Assert.True(File.Exists(path), path);

        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 44);
        Assert.Equal("RIFF"u8.ToArray(), bytes[..4].ToArray());
        Assert.Equal("WAVE"u8.ToArray(), bytes[8..12].ToArray());
        Assert.Equal("fmt "u8.ToArray(), bytes[12..16].ToArray());

        var channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(22));
        var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24));
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(34));
        Assert.Equal(AlertSoundAsset.Channels, channels);
        Assert.Equal(AlertSoundAsset.SampleRate, sampleRate);
        Assert.Equal(AlertSoundAsset.BitsPerSample, bits);

        var dataSize = bytes.Length - 44;
        var durationMs = dataSize * 1000.0 / (sampleRate * (bits / 8.0) * channels);
        Assert.InRange(durationMs, 200, 600);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CheckmkDesktopNotifier.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
