using System.Globalization;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.App.MacOS;

/// <summary>
/// Last-resort log for status-item / panel failures. Written under Application Support.
/// Does not include credentials or secrets.
/// </summary>
public sealed class MacHostErrorLog
{
    public const string FileName = "status-item-error.txt";

    private readonly string _path;
    private readonly object _gate = new();

    public MacHostErrorLog(AppStoragePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _path = Path.Combine(paths.AppDataDirectory, FileName);
    }

    public string FilePath => _path;

    public void Write(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var line = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            + " "
            + MacNativeCallbackGuard.Describe(exception)
            + Environment.NewLine;
        lock (_gate)
        {
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(_path, line);
        }
    }
}
