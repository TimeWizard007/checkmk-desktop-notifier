using System.Text.Json;
using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Rest;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CheckmkCommentMapperTests
{
    [Fact]
    public void Reads_positional_five_tuple_comments()
    {
        using var doc = JsonDocument.Parse("""
            {
              "acknowledged": 1,
              "acknowledgement_type": 2,
              "comments_with_extra_info": [
                [36783, "ITS", "Taken by mwi via Checkmk Desktop Notifier", 4, 1787078432]
              ]
            }
            """);

        var ack = CheckmkCommentMapper.ReadAcknowledgement(doc.RootElement);
        Assert.True(ack.IsAcknowledged);
        Assert.Equal(AcknowledgementType.Sticky, ack.AcknowledgementType);
        Assert.True(ack.IsTakenByNotifier);
        Assert.Equal("mwi", ack.TakenByDisplayName);
    }

    [Fact]
    public void Ignores_object_shaped_comments()
    {
        using var doc = JsonDocument.Parse("""
            {
              "acknowledged": 1,
              "acknowledgement_type": 2,
              "comments_with_extra_info": [
                {"id": 1, "comment": "cdn.v1 take name=\"Michał\"", "entry_type": 4, "entry_time": 1}
              ]
            }
            """);

        var ack = CheckmkCommentMapper.ReadAcknowledgement(doc.RootElement);
        Assert.True(ack.IsAcknowledged);
        Assert.False(ack.IsTakenByNotifier);
        Assert.Null(ack.TakenByDisplayName);
    }

    [Fact]
    public void Maps_flat_five_tuple_when_not_wrapped_in_an_extra_array()
    {
        using var doc = JsonDocument.Parse("""
            {
              "acknowledged": 1,
              "acknowledgement_type": 2,
              "comments_with_extra_info": [
                36783, "ITS", "Taken by Michał via Checkmk Desktop Notifier", 4, 1787078432
              ]
            }
            """);

        var ack = CheckmkCommentMapper.ReadAcknowledgement(doc.RootElement);
        Assert.True(ack.IsTakenByNotifier);
        Assert.Equal("Michał", ack.TakenByDisplayName);
    }

    [Fact]
    public void Joins_multiline_comment_when_checkmk_returns_comment_as_array()
    {
        using var doc = JsonDocument.Parse("""
            {
              "acknowledged": 1,
              "acknowledgement_type": 2,
              "comments_with_extra_info": [
                [36783, "ITS", [
                  "Taken by Michał",
                  "via Checkmk Desktop Notifier",
                  "cdn.v1 take name=\"Michał\""
                ], 4, 1787078432]
              ]
            }
            """);

        var ack = CheckmkCommentMapper.ReadAcknowledgement(doc.RootElement);
        Assert.True(ack.IsTakenByNotifier);
        Assert.Equal("Michał", ack.TakenByDisplayName);
    }

    [Fact]
    public void Does_not_use_author_when_comment_is_generic()
    {
        using var doc = JsonDocument.Parse("""
            {
              "acknowledged": 1,
              "acknowledgement_type": 2,
              "comments_with_extra_info": [
                [1, "ITS", "Known issue", 4, 1234567890]
              ]
            }
            """);

        var ack = CheckmkCommentMapper.ReadAcknowledgement(doc.RootElement);
        Assert.True(ack.IsAcknowledged);
        Assert.False(ack.IsTakenByNotifier);
        Assert.Null(ack.TakenByDisplayName);
    }
}
