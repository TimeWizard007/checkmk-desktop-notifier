using Avalonia;
using Avalonia.Controls;
using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.App.MacOS;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var appData = new MacUserDataDirectory().GetDirectory();
        if (!MacSingleInstanceLock.TryOwn(appData, out var owned) || owned is null)
        {
            MacSingleInstanceLock.SignalExisting(appData);
            return;
        }

        MacRuntime.SingleInstance = owned;
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            MacRuntime.SingleInstance.Dispose();
            MacRuntime.SingleInstance = null;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
