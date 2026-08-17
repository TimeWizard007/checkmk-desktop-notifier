using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CheckmkDesktopNotifier.App;

internal static class AppIcon
{
    public static ImageSource WindowSource { get; } = BitmapFrame.Create(
        new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute));
}
