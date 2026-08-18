using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class CdnTakeCommentTests
{
    [Fact]
    public void Formats_single_line_cdn_comment_with_all_identity_parts()
    {
        Assert.Equal(
            "Taken by Michał via Checkmk Desktop Notifier cdn.v1 take name=\"Michał\"",
            CdnTakeComment.Format("Michał"));
        Assert.DoesNotContain('\n', CdnTakeComment.Format("Michał"));
        Assert.DoesNotContain('\r', CdnTakeComment.Format("Michał"));
    }

    [Fact]
    public void Formats_and_parses_a_valid_cdn_comment()
    {
        var comment = CdnTakeComment.Format("Michał");
        Assert.Contains("Taken by Michał", comment, StringComparison.Ordinal);
        Assert.Contains("via Checkmk Desktop Notifier", comment, StringComparison.Ordinal);
        Assert.Contains("cdn.v1 take name=\"Michał\"", comment, StringComparison.Ordinal);
        Assert.Equal("Michał", CdnTakeComment.TryParseTakenBy(comment));
    }

    [Fact]
    public void Parses_display_name_with_spaces()
    {
        var comment = CdnTakeComment.Format("Jan Kowalski");
        Assert.Equal("Jan Kowalski", CdnTakeComment.TryParseTakenBy(comment));
    }

    [Fact]
    public void Escapes_quotes_and_backslashes()
    {
        const string name = "Ann \"Quote\\x\"";
        var comment = CdnTakeComment.Format(name);
        Assert.Contains("cdn.v1 take name=\"Ann \\\"Quote\\\\x\\\"\"", comment, StringComparison.Ordinal);
        Assert.Equal(name, CdnTakeComment.TryParseTakenBy(comment));
    }

    [Fact]
    public void Malformed_tag_is_ignored()
    {
        Assert.Null(CdnTakeComment.TryParseTakenBy("Taken by x\ncdn.v1 take name=Michał"));
        Assert.Null(CdnTakeComment.TryParseTakenBy("cdn.v1 take name=\""));
        Assert.Null(CdnTakeComment.TryParseTakenBy("cdn.v1 take name=\"\""));
    }

    [Fact]
    public void Normal_checkmk_ack_is_ignored_for_taken_by()
    {
        var comments = new[]
        {
            new CheckmkCommentRecord(1, "ITS", "Acknowledged in GUI", 4, 100)
        };
        var info = CdnTakeComment.Resolve(acknowledged: true, acknowledgementType: 1, comments);
        Assert.True(info.IsAcknowledged);
        Assert.Equal(AcknowledgementType.Normal, info.AcknowledgementType);
        Assert.False(info.IsTakenByNotifier);
        Assert.Null(info.TakenByDisplayName);
    }

    [Fact]
    public void Non_acknowledgement_entry_type_is_ignored()
    {
        var comments = new[]
        {
            new CheckmkCommentRecord(1, "ITS", CdnTakeComment.Format("Michał"), 1, 100)
        };
        var info = CdnTakeComment.Resolve(acknowledged: true, acknowledgementType: 2, comments);
        Assert.True(info.IsAcknowledged);
        Assert.False(info.IsTakenByNotifier);
        Assert.Null(info.TakenByDisplayName);
    }

    [Fact]
    public void Newest_valid_cdn_take_is_selected()
    {
        var comments = new[]
        {
            new CheckmkCommentRecord(1, "ITS", CdnTakeComment.Format("Anna"), 4, 100),
            new CheckmkCommentRecord(2, "ITS", CdnTakeComment.Format("Michał"), 4, 200),
            new CheckmkCommentRecord(3, "ITS", "manual", 4, 300)
        };
        var info = CdnTakeComment.Resolve(acknowledged: true, acknowledgementType: 2, comments);
        Assert.True(info.IsTakenByNotifier);
        Assert.Equal("Michał", info.TakenByDisplayName);
        Assert.Equal(AcknowledgementType.Sticky, info.AcknowledgementType);
    }

    [Fact]
    public void Generic_ack_without_cdn_comment()
    {
        var info = CdnTakeComment.Resolve(acknowledged: true, acknowledgementType: 2, Array.Empty<CheckmkCommentRecord>());
        Assert.True(info.IsAcknowledged);
        Assert.Equal(AcknowledgementType.Sticky, info.AcknowledgementType);
        Assert.False(info.IsTakenByNotifier);
        Assert.Null(info.TakenByDisplayName);
    }

    [Fact]
    public void Author_is_never_used_as_taken_by()
    {
        var comments = new[]
        {
            new CheckmkCommentRecord(1, "ITS", "Taken by ITS in GUI", 4, 100)
        };
        var info = CdnTakeComment.Resolve(acknowledged: true, acknowledgementType: 2, comments);
        Assert.Null(info.TakenByDisplayName);
        Assert.False(info.IsTakenByNotifier);
    }

    [Fact]
    public void Parses_flattened_checkmk_rest_comment_without_machine_line()
    {
        Assert.Equal(
            "mwi",
            CdnTakeComment.TryParseTakenBy("Taken by mwi via Checkmk Desktop Notifier"));
        Assert.Equal(
            "Michał",
            CdnTakeComment.TryParseTakenBy("Taken by Michał\nvia Checkmk Desktop Notifier"));
        Assert.Equal(
            "Michał",
            CdnTakeComment.TryParseTakenBy("  Taken by   Michał  \r\nvia Checkmk Desktop Notifier  "));
    }

    [Fact]
    public void First_line_only_without_cdn_via_stays_generic()
    {
        Assert.Null(CdnTakeComment.TryParseTakenBy("Taken by Michał"));
    }

    [Fact]
    public void Unacknowledged_is_none()
    {
        var comments = new[]
        {
            new CheckmkCommentRecord(1, "ITS", CdnTakeComment.Format("Michał"), 4, 100)
        };
        var info = CdnTakeComment.Resolve(acknowledged: false, acknowledgementType: 2, comments);
        Assert.Equal(CheckmkAcknowledgementInfo.None, info);
    }

    [Fact]
    public void Explicit_acknowledgement_type_zero_clears_leftover_cdn_comments()
    {
        var comments = new[]
        {
            new CheckmkCommentRecord(1, "ITS", CdnTakeComment.Format("Michał"), 4, 100)
        };
        var info = CdnTakeComment.Resolve(acknowledged: true, acknowledgementType: 0, comments);
        Assert.Equal(CheckmkAcknowledgementInfo.None, info);
    }

    [Theory]
    [InlineData(1, AcknowledgementType.Normal)]
    [InlineData(2, AcknowledgementType.Sticky)]
    [InlineData(0, AcknowledgementType.None)]
    [InlineData(9, AcknowledgementType.None)]
    public void Maps_acknowledgement_type(int value, AcknowledgementType expected)
    {
        Assert.Equal(expected, CdnTakeComment.MapAcknowledgementType(value));
    }
}
