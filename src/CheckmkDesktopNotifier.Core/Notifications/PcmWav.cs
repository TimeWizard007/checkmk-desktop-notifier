using System.Buffers.Binary;
using System.Text;

namespace CheckmkDesktopNotifier.Core.Notifications;

public enum NotificationSoundSource
{
    Default = 0,
    Custom = 1
}

public enum PcmWavError
{
    None = 0,
    TooSmall,
    NotRiff,
    NotWave,
    MissingFmt,
    NotPcm,
    UnsupportedBits,
    UnsupportedChannels,
    UnsupportedSampleRate,
    MissingData,
    EmptyData,
    TooLarge,
    TooLong
}

public sealed class PcmWav
{
    public required int SampleRate { get; init; }

    public required ushort Channels { get; init; }

    public required ushort BitsPerSample { get; init; }

    public required byte[] Pcm { get; init; }
}

public static class PcmWavLimits
{
    public const int MaxFileBytes = 2 * 1024 * 1024;

    public const double MaxDurationSeconds = 5;

    public const int MinSampleRate = 8000;

    public const int MaxSampleRate = 48000;

    public const int DefaultVolumePercent = 30;
}

public static class PcmWavParser
{
    public static bool TryParse(ReadOnlySpan<byte> bytes, out PcmWav wav, out PcmWavError error)
    {
        wav = null!;
        if (bytes.Length < 12)
        {
            error = PcmWavError.TooSmall;
            return false;
        }

        if (bytes.Length > PcmWavLimits.MaxFileBytes)
        {
            error = PcmWavError.TooLarge;
            return false;
        }

        if (!bytes[..4].SequenceEqual("RIFF"u8) || !bytes.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            error = bytes[..4].SequenceEqual("RIFF"u8) ? PcmWavError.NotWave : PcmWavError.NotRiff;
            return false;
        }

        ushort format = 0, channels = 0, bits = 0;
        int sampleRate = 0;
        var hasFmt = false;
        byte[]? pcm = null;
        var offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            var chunkId = Encoding.ASCII.GetString(bytes.Slice(offset, 4));
            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4));
            if (chunkSize < 0 || offset + 8 + chunkSize > bytes.Length)
            {
                break;
            }

            var payload = bytes.Slice(offset + 8, chunkSize);
            if (chunkId == "fmt " && payload.Length >= 16)
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(payload);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]);
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(payload[14..]);
                hasFmt = true;
            }
            else if (chunkId == "data")
            {
                pcm = payload.ToArray();
            }

            offset += 8 + chunkSize + (chunkSize & 1);
        }

        if (!hasFmt)
        {
            error = PcmWavError.MissingFmt;
            return false;
        }

        if (format != 1)
        {
            error = PcmWavError.NotPcm;
            return false;
        }

        if (channels is not (1 or 2))
        {
            error = PcmWavError.UnsupportedChannels;
            return false;
        }

        if (bits is not (8 or 16))
        {
            error = PcmWavError.UnsupportedBits;
            return false;
        }

        if (sampleRate < PcmWavLimits.MinSampleRate || sampleRate > PcmWavLimits.MaxSampleRate)
        {
            error = PcmWavError.UnsupportedSampleRate;
            return false;
        }

        if (pcm is null)
        {
            error = PcmWavError.MissingData;
            return false;
        }

        if (pcm.Length == 0)
        {
            error = PcmWavError.EmptyData;
            return false;
        }

        var bytesPerSample = bits / 8;
        var frameBytes = Math.Max(1, channels * bytesPerSample);
        var duration = pcm.Length / (double)(sampleRate * frameBytes);
        if (duration > PcmWavLimits.MaxDurationSeconds)
        {
            error = PcmWavError.TooLong;
            return false;
        }

        wav = new PcmWav
        {
            SampleRate = sampleRate,
            Channels = channels,
            BitsPerSample = bits,
            Pcm = pcm
        };
        error = PcmWavError.None;
        return true;
    }
}

public static class PcmWavWriter
{
    public static byte[] Write(PcmWav wav)
    {
        ArgumentNullException.ThrowIfNull(wav);
        var pcm = wav.Pcm;
        var dataSize = pcm.Length;
        var file = new byte[44 + dataSize];
        "RIFF"u8.CopyTo(file);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(4), 36 + dataSize);
        "WAVE"u8.CopyTo(file.AsSpan(8));
        "fmt "u8.CopyTo(file.AsSpan(12));
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(22), wav.Channels);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(24), wav.SampleRate);
        var blockAlign = (ushort)(wav.Channels * (wav.BitsPerSample / 8));
        var byteRate = wav.SampleRate * blockAlign;
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(28), byteRate);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(32), blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(34), wav.BitsPerSample);
        "data"u8.CopyTo(file.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(40), dataSize);
        pcm.CopyTo(file, 44);
        return file;
    }
}

public static class PcmWavVolume
{
    public static int ClampPercent(int percent) => Math.Clamp(percent, 0, 100);

    public static short ScaleInt16(short sample, int volumePercent)
    {
        var factor = ClampPercent(volumePercent) / 100.0;
        var scaled = (long)Math.Round(sample * factor);
        if (scaled > short.MaxValue)
        {
            return short.MaxValue;
        }

        if (scaled < short.MinValue)
        {
            return short.MinValue;
        }

        return (short)scaled;
    }

    public static byte ScaleUInt8(byte sample, int volumePercent)
    {
        var factor = ClampPercent(volumePercent) / 100.0;
        var centered = sample - 128;
        var scaled = (int)Math.Round(centered * factor);
        scaled = Math.Clamp(scaled, -128, 127);
        return (byte)(scaled + 128);
    }

    public static byte[] Apply(PcmWav wav, int volumePercent)
    {
        ArgumentNullException.ThrowIfNull(wav);
        var volume = ClampPercent(volumePercent);
        var pcm = (byte[])wav.Pcm.Clone();
        if (wav.BitsPerSample == 16)
        {
            for (var i = 0; i + 1 < pcm.Length; i += 2)
            {
                var sample = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i, 2));
                BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i, 2), ScaleInt16(sample, volume));
            }
        }
        else
        {
            for (var i = 0; i < pcm.Length; i++)
            {
                pcm[i] = ScaleUInt8(pcm[i], volume);
            }
        }

        return PcmWavWriter.Write(new PcmWav
        {
            SampleRate = wav.SampleRate,
            Channels = wav.Channels,
            BitsPerSample = wav.BitsPerSample,
            Pcm = pcm
        });
    }
}

/// <summary>
/// Chooses Default vs Custom PCM WAV and applies application volume. Does not use OS mixer APIs.
/// </summary>
public static class NotificationSoundMixer
{
    public static byte[] Mix(
        ReadOnlySpan<byte> bundledDefault,
        ReadOnlySpan<byte> customBytes,
        NotificationSoundSource source,
        int volumePercent)
    {
        var chosen = bundledDefault;
        if (source == NotificationSoundSource.Custom
            && PcmWavParser.TryParse(customBytes, out _, out _))
        {
            chosen = customBytes;
        }

        if (!PcmWavParser.TryParse(chosen, out var wav, out _))
        {
            if (!PcmWavParser.TryParse(bundledDefault, out wav, out _))
            {
                return [];
            }
        }

        return PcmWavVolume.Apply(wav, volumePercent);
    }
}
