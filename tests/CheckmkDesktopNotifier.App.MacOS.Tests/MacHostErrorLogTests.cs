using CheckmkDesktopNotifier.App.MacOS;
using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.App.MacOS.Tests;

public sealed class MacHostErrorLogTests
{
    [Fact]
    public void Write_redacts_bearer_and_does_not_include_the_secret()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cdn-mac-error-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var log = new MacHostErrorLog(new AppStoragePaths(directory));
            log.Write(new InvalidOperationException("Bearer secret-value"));
            var text = File.ReadAllText(log.FilePath);
            Assert.Contains("InvalidOperationException", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Bearer", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-value", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
