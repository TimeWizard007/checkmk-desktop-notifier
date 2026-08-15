using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.App.Converters;

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Severity severity
            ? severity switch
            {
                Severity.Critical => Application.Current.FindResource("CriticalBrush"),
                Severity.Warning => Application.Current.FindResource("WarningBrush"),
                Severity.Unknown => Application.Current.FindResource("UnknownBrush"),
                _ => Application.Current.FindResource("MutedBrush")
            }
            : Application.Current.FindResource("MutedBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
