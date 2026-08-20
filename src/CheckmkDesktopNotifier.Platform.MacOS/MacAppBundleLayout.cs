using CheckmkDesktopNotifier.Core.Autostart;

namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// Filesystem layout of a macOS <c>.app</c> bundle. Used for Start at Login and
/// to decide whether UserNotifications APIs are safe to touch.
/// </summary>
public sealed class MacAppBundleLayout
{
    public const string Identifier = "com.timewizard.checkmkdesktopnotifier";

    public const string AppFolderName = "Checkmk Desktop Notifier.app";

    public const string ExecutableName = "CheckmkDesktopNotifier.MacOS";

    public const string IconFileName = "CheckmkDesktopNotifier.icns";

    public static MacAppBundleLayout None { get; } = new(false, null, null, null);

    public MacAppBundleLayout(
        bool isApplicationBundle,
        string? bundlePath,
        string? bundleIdentifier,
        string? processPath)
    {
        IsApplicationBundle = isApplicationBundle;
        BundlePath = bundlePath;
        BundleIdentifier = bundleIdentifier;
        ProcessPath = processPath;
    }

    public bool IsApplicationBundle { get; }

    public string? BundlePath { get; }

    public string? BundleIdentifier { get; }

    public string? ProcessPath { get; }

    public string LaunchPath =>
        IsApplicationBundle && !string.IsNullOrWhiteSpace(BundlePath)
            ? BundlePath
            : ProcessPath ?? string.Empty;

    public static MacAppBundleLayout Detect(string? processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return None;
        }

        var full = Path.GetFullPath(processPath.Trim());
        if (IsAppBundleDirectory(full))
        {
            return FromBundlePath(full, full);
        }

        var macos = Path.GetDirectoryName(full);
        var contents = macos is null ? null : Path.GetDirectoryName(macos);
        var bundle = contents is null ? null : Path.GetDirectoryName(contents);
        if (macos is not null
            && contents is not null
            && bundle is not null
            && string.Equals(Path.GetFileName(macos), "MacOS", StringComparison.Ordinal)
            && string.Equals(Path.GetFileName(contents), "Contents", StringComparison.Ordinal)
            && IsAppBundleDirectory(bundle))
        {
            return FromBundlePath(bundle, full);
        }

        return new MacAppBundleLayout(false, null, null, full);
    }

    public static bool IsAppBundleDirectory(string path) =>
        path.EndsWith(".app", StringComparison.OrdinalIgnoreCase);

    private static MacAppBundleLayout FromBundlePath(string bundlePath, string processPath)
    {
        var plist = Path.Combine(bundlePath, "Contents", "Info.plist");
        var identifier = File.Exists(plist)
            ? MacAppInfoPlist.TryReadIdentifier(File.ReadAllText(plist))
            : null;
        return new MacAppBundleLayout(true, bundlePath, identifier, processPath);
    }
}

public static class MacNotificationEnvironment
{
    public static bool ShouldCallCurrentNotificationCenter(string? bundleIdentifier) =>
        !string.IsNullOrWhiteSpace(bundleIdentifier);

    public static bool IsSafeToInitializeNative(string? liveBundleIdentifier, MacAppBundleLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (ShouldCallCurrentNotificationCenter(liveBundleIdentifier))
        {
            return true;
        }

        return layout.IsApplicationBundle
               && ShouldCallCurrentNotificationCenter(layout.BundleIdentifier);
    }

    public static MacNotificationBackend SelectBackend(
        bool isMacOS,
        string? liveBundleIdentifier,
        MacAppBundleLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!isMacOS)
        {
            return MacNotificationBackend.Recording;
        }

        return IsSafeToInitializeNative(liveBundleIdentifier, layout)
            ? MacNotificationBackend.Native
            : MacNotificationBackend.Disabled;
    }
}

public enum MacNotificationBackend
{
    Recording = 0,
    Disabled = 1,
    Native = 2
}

public static class MacAppInfoPlist
{
    public static string BuildXml(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        var identifier = MacAppBundleLayout.Identifier;
        var executable = MacAppBundleLayout.ExecutableName;
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
            + "<plist version=\"1.0\">\n"
            + "<dict>\n"
            + "\t<key>CFBundleDevelopmentRegion</key>\n"
            + "\t<string>en</string>\n"
            + "\t<key>CFBundleDisplayName</key>\n"
            + "\t<string>Checkmk Desktop Notifier</string>\n"
            + "\t<key>CFBundleExecutable</key>\n"
            + "\t<string>" + executable + "</string>\n"
            + "\t<key>CFBundleIdentifier</key>\n"
            + "\t<string>" + identifier + "</string>\n"
            + "\t<key>CFBundleInfoDictionaryVersion</key>\n"
            + "\t<string>6.0</string>\n"
            + "\t<key>CFBundleName</key>\n"
            + "\t<string>Checkmk Desktop Notifier</string>\n"
            + "\t<key>CFBundlePackageType</key>\n"
            + "\t<string>APPL</string>\n"
            + "\t<key>CFBundleIconFile</key>\n"
            + "\t<string>" + MacAppBundleLayout.IconFileName + "</string>\n"
            + "\t<key>CFBundleShortVersionString</key>\n"
            + "\t<string>" + version + "</string>\n"
            + "\t<key>CFBundleVersion</key>\n"
            + "\t<string>" + version + "</string>\n"
            + "\t<key>LSMinimumSystemVersion</key>\n"
            + "\t<string>12.0</string>\n"
            + "\t<key>LSUIElement</key>\n"
            + "\t<true/>\n"
            + "\t<key>NSHighResolutionCapable</key>\n"
            + "\t<true/>\n"
            + "</dict>\n"
            + "</plist>\n";
    }

    public static string? TryReadIdentifier(string plistXml)
    {
        if (string.IsNullOrWhiteSpace(plistXml))
        {
            return null;
        }

        var key = plistXml.IndexOf("CFBundleIdentifier", StringComparison.Ordinal);
        if (key < 0)
        {
            return null;
        }

        var open = plistXml.IndexOf("<string>", key, StringComparison.Ordinal);
        if (open < 0)
        {
            return null;
        }

        open += "<string>".Length;
        var close = plistXml.IndexOf("</string>", open, StringComparison.Ordinal);
        if (close < 0)
        {
            return null;
        }

        var value = plistXml[open..close].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public static class MacAppBundlePackager
{
    public static string Package(string publishDirectory, string appPath, string version, string? iconPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publishDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(appPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (!Directory.Exists(publishDirectory))
        {
            throw new DirectoryNotFoundException(publishDirectory);
        }

        var contents = Path.Combine(appPath, "Contents");
        var macos = Path.Combine(contents, "MacOS");
        var resources = Path.Combine(contents, "Resources");
        Directory.CreateDirectory(macos);
        Directory.CreateDirectory(resources);

        foreach (var entry in Directory.GetFileSystemEntries(publishDirectory))
        {
            var name = Path.GetFileName(entry);
            if (name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destination = Path.Combine(macos, name);
            if (Directory.Exists(entry))
            {
                CopyDirectory(entry, destination);
            }
            else
            {
                File.Copy(entry, destination, overwrite: true);
            }
        }

        File.WriteAllText(Path.Combine(contents, "Info.plist"), MacAppInfoPlist.BuildXml(version));
        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
        {
            File.Copy(iconPath, Path.Combine(resources, MacAppBundleLayout.IconFileName), overwrite: true);
        }
        var executable = Path.Combine(macos, MacAppBundleLayout.ExecutableName);
        if (File.Exists(executable) && !OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(
                    executable,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            catch (Exception)
            {
            }
        }

        return appPath;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            if (file.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}

public sealed class MacApplicationExecutable : IApplicationExecutable
{
    public string GetPath()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("The current executable path is not available.");
        }

        return MacAppBundleLayout.Detect(path).LaunchPath;
    }
}
