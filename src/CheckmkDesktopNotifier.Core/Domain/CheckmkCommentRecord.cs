namespace CheckmkDesktopNotifier.Core.Domain;

/// <summary>
/// One Checkmk comment tuple from <c>comments_with_extra_info</c>:
/// <c>[comment_id, author, comment, entry_type, entry_time]</c>.
/// The Checkmk author is the automation account and is never used as Taken-by identity.
/// </summary>
public readonly record struct CheckmkCommentRecord(
    long Id,
    string Author,
    string Comment,
    int EntryType,
    long EntryTime);
