namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// Builds the <c>/usr/bin/open</c> invocation used to open a URI in the default browser.
/// </summary>
public static class MacOpenCommand
{
    public const string Executable = "/usr/bin/open";

    public static IReadOnlyList<string> BuildArguments(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("URI must be absolute.", nameof(uri));
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("URI must not contain credentials.", nameof(uri));
        }

        return [uri.AbsoluteUri];
    }
}
