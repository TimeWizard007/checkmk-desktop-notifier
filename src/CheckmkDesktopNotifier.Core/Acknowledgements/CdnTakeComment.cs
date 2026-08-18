using System.Text;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Acknowledgements;

/// <summary>
/// Formats and parses Checkmk Desktop Notifier Take acknowledgement comments.
/// Identity comes from the machine line, never from the Checkmk author field.
/// </summary>
public static class CdnTakeComment
{
    public const int AcknowledgementEntryType = 4;
    public const string MachineMarker = "cdn.v1 take name=";

    public static string Format(string displayName)
    {
        var name = TakeDisplayName.Normalize(displayName)
                   ?? throw new ArgumentException("Display name is required.", nameof(displayName));
        // Checkmk RAW 2.4 acknowledgement comments are single-line. A `\n` comment is stored as
        // only "Taken by {name}" (live GO-S11). Keep all three identity parts on one line.
        return $"Taken by {name} via Checkmk Desktop Notifier {MachineMarker}\"{Escape(name)}\"";
    }

    public static string? TryParseTakenBy(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        return TryParseMachineName(comment) ?? TryParseHumanTakenBy(comment);
    }

    public static AcknowledgementType MapAcknowledgementType(int? value) =>
        value switch
        {
            1 => AcknowledgementType.Normal,
            2 => AcknowledgementType.Sticky,
            _ => AcknowledgementType.None
        };

    public static CheckmkAcknowledgementInfo Resolve(
        bool acknowledged,
        int? acknowledgementType,
        IEnumerable<CheckmkCommentRecord>? comments)
    {
        var type = MapAcknowledgementType(acknowledgementType);
        // acknowledged=0, or an explicit acknowledgement_type=0, means no active ACK
        // even if leftover CDN comments remain on the object after delete.
        if (!acknowledged || acknowledgementType == 0)
        {
            return CheckmkAcknowledgementInfo.None;
        }

        string? takenBy = null;
        long bestTime = long.MinValue;
        long bestId = long.MinValue;
        if (comments is not null)
        {
            foreach (var comment in comments)
            {
                if (comment.EntryType != AcknowledgementEntryType)
                {
                    continue;
                }

                var name = TryParseTakenBy(comment.Comment);
                if (name is null)
                {
                    continue;
                }

                if (comment.EntryTime > bestTime
                    || (comment.EntryTime == bestTime && comment.Id > bestId))
                {
                    bestTime = comment.EntryTime;
                    bestId = comment.Id;
                    takenBy = name;
                }
            }
        }

        return new CheckmkAcknowledgementInfo(
            IsAcknowledged: true,
            AcknowledgementType: type,
            TakenByDisplayName: takenBy,
            IsTakenByNotifier: takenBy is not null);
    }

    private static string? TryParseMachineName(string comment)
    {
        var index = comment.IndexOf(MachineMarker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var after = comment.AsSpan(index + MachineMarker.Length);
        if (!TryReadQuoted(after, out var raw))
        {
            return null;
        }

        return TakeDisplayName.Normalize(raw);
    }

    /// <summary>
    /// Checkmk 2.4 may store or return the human lines without the machine tag
    /// (GUI first lines, or a flattened "Taken by {name} via Checkmk Desktop Notifier").
    /// Require the CDN "via" phrase so a generic "Taken by …" ACK stays generic.
    /// </summary>
    private static string? TryParseHumanTakenBy(string comment)
    {
        var normalized = NormalizeWhitespace(comment);
        const string prefix = "Taken by ";
        const string via = " via Checkmk Desktop Notifier";
        var prefixIndex = normalized.IndexOf(prefix, StringComparison.Ordinal);
        if (prefixIndex < 0)
        {
            return null;
        }

        var nameStart = prefixIndex + prefix.Length;
        var viaIndex = normalized.IndexOf(via, nameStart, StringComparison.Ordinal);
        if (viaIndex < 0)
        {
            return null;
        }

        return TakeDisplayName.Normalize(normalized[nameStart..viaIndex]);
    }

    private static string NormalizeWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWhitespace = false;
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousWhitespace)
                {
                    builder.Append(' ');
                    previousWhitespace = true;
                }

                continue;
            }

            builder.Append(c);
            previousWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static bool TryReadQuoted(ReadOnlySpan<char> text, out string unescaped)
    {
        unescaped = string.Empty;
        if (text.IsEmpty || text[0] != '"')
        {
            return false;
        }

        var builder = new StringBuilder();
        for (var i = 1; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\')
            {
                if (i + 1 >= text.Length)
                {
                    return false;
                }

                builder.Append(text[i + 1]);
                i++;
                continue;
            }

            if (c == '"')
            {
                unescaped = builder.ToString();
                return true;
            }

            builder.Append(c);
        }

        return false;
    }
}
