using CheckmkDesktopNotifier.Core.Autostart;

namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// Per-user Start at Login via a LaunchAgent plist. When the process lives in a
/// <c>.app</c>, the agent launches that bundle with <c>/usr/bin/open</c> so the
/// app starts with a real bundle identifier. <c>SMAppService</c> login items
/// require a signed <c>.app</c> and are not faked here.
/// </summary>
public static class MacLoginItemCapability
{
    public const string Label = "com.timewizard.checkmkdesktopnotifier";

    public const string FileName = "com.timewizard.checkmkdesktopnotifier.plist";

    public const string Mechanism = "LaunchAgent";

    public static bool RequiresAppBundle => false;

    public static string Limitation =>
        "Start at Login registers a per-user LaunchAgent. When running from the "
        + ".app bundle it launches that app with /usr/bin/open. SMAppService login "
        + "items require a signed .app and are deferred until packaging/signing.";
}

public static class MacLaunchAgentPlist
{
    public static string Build(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var path = AutostartCommand.Unquote(executablePath);
        if (AutostartCommand.ContainsDisallowedPayload(path))
        {
            throw new InvalidOperationException("Autostart command must not include credentials.");
        }

        var layout = MacAppBundleLayout.Detect(path);
        var label = MacLoginItemCapability.Label;
        var arguments = layout.IsApplicationBundle && !string.IsNullOrWhiteSpace(layout.BundlePath)
            ? "\t\t<string>" + Escape(MacOpenCommand.Executable) + "</string>\n"
              + "\t\t<string>" + Escape(layout.BundlePath) + "</string>\n"
            : "\t\t<string>" + Escape(path) + "</string>\n";
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
            + "<plist version=\"1.0\">\n"
            + "<dict>\n"
            + "\t<key>Label</key>\n"
            + "\t<string>" + label + "</string>\n"
            + "\t<key>ProgramArguments</key>\n"
            + "\t<array>\n"
            + arguments
            + "\t</array>\n"
            + "\t<key>RunAtLoad</key>\n"
            + "\t<true/>\n"
            + "\t<key>KeepAlive</key>\n"
            + "\t<false/>\n"
            + "\t<key>LimitLoadToSessionType</key>\n"
            + "\t<string>Aqua</string>\n"
            + "</dict>\n"
            + "</plist>\n";
    }

    public static string? TryReadExecutable(string plistXml)
    {
        if (string.IsNullOrWhiteSpace(plistXml))
        {
            return null;
        }

        var label = plistXml.IndexOf(MacLoginItemCapability.Label, StringComparison.Ordinal);
        if (label < 0)
        {
            return null;
        }

        var array = plistXml.IndexOf("<array>", label, StringComparison.Ordinal);
        var arrayEnd = array < 0 ? -1 : plistXml.IndexOf("</array>", array, StringComparison.Ordinal);
        if (array < 0 || arrayEnd < 0)
        {
            return null;
        }

        var values = new List<string>();
        var cursor = array;
        const string open = "<string>";
        const string close = "</string>";
        while (true)
        {
            var start = plistXml.IndexOf(open, cursor, StringComparison.Ordinal);
            if (start < 0 || start >= arrayEnd)
            {
                break;
            }

            start += open.Length;
            var end = plistXml.IndexOf(close, start, StringComparison.Ordinal);
            if (end < 0 || end > arrayEnd)
            {
                break;
            }

            var value = Unescape(plistXml[start..end].Trim());
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }

            cursor = end + close.Length;
        }

        if (values.Count == 0)
        {
            return null;
        }

        if (string.Equals(values[0], MacOpenCommand.Executable, StringComparison.Ordinal))
        {
            var app = values.LastOrDefault(item => item.EndsWith(".app", StringComparison.OrdinalIgnoreCase));
            return app ?? values[^1];
        }

        return values[0];
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string Unescape(string value) =>
        value.Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&amp;", "&", StringComparison.Ordinal);
}

public sealed class MacLaunchAgentAutostartStore : IAutostartStore
{
    private readonly string _plistPath;

    public MacLaunchAgentAutostartStore()
        : this(DefaultPlistPath())
    {
    }

    public MacLaunchAgentAutostartStore(string plistPath)
    {
        if (string.IsNullOrWhiteSpace(plistPath))
        {
            throw new ArgumentException("LaunchAgent path must not be empty.", nameof(plistPath));
        }

        _plistPath = plistPath;
    }

    public string PlistPath => _plistPath;

    public AutostartRegistration? Read()
    {
        if (!File.Exists(_plistPath))
        {
            return null;
        }

        var xml = File.ReadAllText(_plistPath);
        var executable = MacLaunchAgentPlist.TryReadExecutable(xml);
        return executable is null ? null : new AutostartRegistration(AutostartCommand.Format(executable));
    }

    public void Write(AutostartRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var directory = Path.GetDirectoryName(_plistPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_plistPath, MacLaunchAgentPlist.Build(registration.CommandLine));
    }

    public void Delete()
    {
        if (File.Exists(_plistPath))
        {
            File.Delete(_plistPath);
        }
    }

    private static string DefaultPlistPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Library", "LaunchAgents", MacLoginItemCapability.FileName);
    }
}
