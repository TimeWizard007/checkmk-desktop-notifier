using System.Text.Json;
using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

internal static class CheckmkCommentMapper
{
    public static IReadOnlyList<CheckmkCommentRecord> Read(JsonElement extensions)
    {
        if (!CheckmkJson.TryGetProperty(extensions, "comments_with_extra_info", out var comments))
        {
            return Array.Empty<CheckmkCommentRecord>();
        }

        return ReadCommentsValue(comments);
    }

    public static CheckmkAcknowledgementInfo ReadAcknowledgement(JsonElement extensions)
    {
        var acknowledged = (JsonValueParser.ReadInt(extensions, "acknowledged") ?? 0) != 0;
        var type = JsonValueParser.ReadInt(extensions, "acknowledgement_type");
        return CdnTakeComment.Resolve(acknowledged, type, Read(extensions));
    }

    private static IReadOnlyList<CheckmkCommentRecord> ReadCommentsValue(JsonElement comments)
    {
        if (comments.ValueKind == JsonValueKind.String)
        {
            var raw = comments.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<CheckmkCommentRecord>();
            }

            try
            {
                using var inner = JsonDocument.Parse(raw, CheckmkJson.DocumentOptions);
                return ReadArray(inner.RootElement);
            }
            catch (JsonException)
            {
                return Array.Empty<CheckmkCommentRecord>();
            }
        }

        return ReadArray(comments);
    }

    private static IReadOnlyList<CheckmkCommentRecord> ReadArray(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CheckmkCommentRecord>();
        }

        var list = new List<CheckmkCommentRecord>();
        foreach (var item in value.EnumerateArray())
        {
            if (TryMap(item, out var record))
            {
                list.Add(record);
            }
        }

        if (list.Count == 0 && TryMap(value, out var single))
        {
            list.Add(single);
        }

        return list;
    }

    private static bool TryMap(JsonElement item, out CheckmkCommentRecord record)
    {
        record = default;
        if (item.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        JsonElement idElement = default;
        JsonElement authorElement = default;
        JsonElement commentElement = default;
        JsonElement typeElement = default;
        JsonElement timeElement = default;
        var index = 0;
        foreach (var value in item.EnumerateArray())
        {
            switch (index)
            {
                case 0:
                    idElement = value;
                    break;
                case 1:
                    authorElement = value;
                    break;
                case 2:
                    commentElement = value;
                    break;
                case 3:
                    typeElement = value;
                    break;
                case 4:
                    timeElement = value;
                    break;
            }

            index++;
            if (index > 4)
            {
                break;
            }
        }

        if (index < 5)
        {
            return false;
        }

        var id = JsonValueParser.ReadInt64(idElement);
        var comment = ReadCommentText(commentElement);
        var entryType = JsonValueParser.ReadInt(typeElement);
        var entryTime = JsonValueParser.ReadInt64(timeElement);
        if (id is null || comment is null || entryType is null || entryTime is null)
        {
            return false;
        }

        record = new CheckmkCommentRecord(
            id.Value,
            JsonValueParser.ReadString(authorElement) ?? string.Empty,
            comment,
            entryType.Value,
            entryTime.Value);
        return true;
    }

    private static string? ReadCommentText(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.Array => JoinCommentLines(value),
            _ => null
        };

    private static string? JoinCommentLines(JsonElement array)
    {
        var parts = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            var part = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Number => item.ToString(),
                _ => null
            };
            if (!string.IsNullOrEmpty(part))
            {
                parts.Add(part);
            }
        }

        return parts.Count == 0 ? null : string.Join("\n", parts);
    }
}
