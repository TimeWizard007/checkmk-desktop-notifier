using System.Buffers.Binary;
using CheckmkDesktopNotifier.Core.Notifications;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class PcmWavTests
{
    [Fact]
    public void Default_source_uses_bundled_bytes()
    {
        var bundled = CreateTone(amplitude: 10000);
        var custom = CreateTone(amplitude: 20000);
        var mixed = NotificationSoundMixer.Mix(bundled, custom, NotificationSoundSource.Default, 100);
        Assert.True(PcmWavParser.TryParse(mixed, out var wav, out _));
        Assert.Equal(10000, ReadFirstSample(wav));
    }

    [Fact]
    public void Custom_source_uses_imported_bytes()
    {
        var bundled = CreateTone(amplitude: 10000);
        var custom = CreateTone(amplitude: 20000);
        var mixed = NotificationSoundMixer.Mix(bundled, custom, NotificationSoundSource.Custom, 100);
        Assert.True(PcmWavParser.TryParse(mixed, out var wav, out _));
        Assert.Equal(20000, ReadFirstSample(wav));
    }

    [Fact]
    public void Missing_custom_falls_back_to_default()
    {
        var bundled = CreateTone(amplitude: 10000);
        var mixed = NotificationSoundMixer.Mix(bundled, [], NotificationSoundSource.Custom, 100);
        Assert.True(PcmWavParser.TryParse(mixed, out var wav, out _));
        Assert.Equal(10000, ReadFirstSample(wav));
    }

    [Fact]
    public void Invalid_wav_is_rejected()
    {
        Assert.False(PcmWavParser.TryParse("this is not a riff wave file"u8, out _, out var error));
        Assert.Equal(PcmWavError.NotRiff, error);
    }

    [Fact]
    public void Non_pcm_wav_is_rejected()
    {
        var bytes = CreateTone(amplitude: 1000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), 3);
        Assert.False(PcmWavParser.TryParse(bytes, out _, out var error));
        Assert.Equal(PcmWavError.NotPcm, error);
    }

    [Fact]
    public void Volume_100_keeps_amplitude()
    {
        var wav = Parse(CreateTone(amplitude: 10000));
        var scaled = PcmWavVolume.Apply(wav, 100);
        Assert.Equal(10000, ReadFirstSample(Parse(scaled)));
    }

    [Fact]
    public void Volume_30_reduces_amplitude()
    {
        var wav = Parse(CreateTone(amplitude: 10000));
        var scaled = PcmWavVolume.Apply(wav, 30);
        Assert.Equal(3000, ReadFirstSample(Parse(scaled)));
    }

    [Fact]
    public void Volume_0_is_silent()
    {
        var wav = Parse(CreateTone(amplitude: 10000));
        var scaled = PcmWavVolume.Apply(wav, 0);
        Assert.Equal(0, ReadFirstSample(Parse(scaled)));
    }

    [Fact]
    public void Amplitude_is_clamped_to_int16_range()
    {
        Assert.Equal(short.MaxValue, PcmWavVolume.ScaleInt16(short.MaxValue, 100));
        Assert.Equal(short.MinValue, PcmWavVolume.ScaleInt16(short.MinValue, 100));
        Assert.Equal(0, PcmWavVolume.ScaleInt16(short.MaxValue, 0));
        Assert.Equal(128, PcmWavVolume.ScaleUInt8(255, 0));
        Assert.Equal(255, PcmWavVolume.ScaleUInt8(255, 100));
    }

    [Fact]
    public void Mixer_applies_configured_volume_to_selected_source()
    {
        var bundled = CreateTone(amplitude: 10000);
        var custom = CreateTone(amplitude: 20000);
        var mixed = NotificationSoundMixer.Mix(bundled, custom, NotificationSoundSource.Custom, 30);
        Assert.Equal(6000, ReadFirstSample(Parse(mixed)));
    }

    [Fact]
    public void Volume_percent_is_clamped()
    {
        Assert.Equal(0, PcmWavVolume.ClampPercent(-10));
        Assert.Equal(100, PcmWavVolume.ClampPercent(250));
        Assert.Equal(30, PcmWavVolume.ClampPercent(30));
    }

    private static byte[] CreateTone(short amplitude, int sampleRate = 22050, int samples = 220)
    {
        var pcm = new byte[samples * 2];
        for (var i = 0; i < samples; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2, 2), amplitude);
        }

        return PcmWavWriter.Write(new PcmWav
        {
            SampleRate = sampleRate,
            Channels = 1,
            BitsPerSample = 16,
            Pcm = pcm
        });
    }

    private static PcmWav Parse(byte[] bytes)
    {
        Assert.True(PcmWavParser.TryParse(bytes, out var wav, out var error), error.ToString());
        return wav;
    }

    private static short ReadFirstSample(PcmWav wav) =>
        BinaryPrimitives.ReadInt16LittleEndian(wav.Pcm.AsSpan(0, 2));
}
