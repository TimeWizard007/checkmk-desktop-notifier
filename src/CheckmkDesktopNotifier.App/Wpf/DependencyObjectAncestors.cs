using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CheckmkDesktopNotifier.Core;

namespace CheckmkDesktopNotifier.App.Wpf;

public static class DependencyObjectAncestors
{
    public static DependencyObject? GetParent(DependencyObject current)
    {
        ArgumentNullException.ThrowIfNull(current);

        return WpfParentKind.For(
            isContentElement: current is ContentElement,
            isVisual: current is Visual,
            isVisual3D: current is Visual3D) switch
        {
            ParentLookup.Content => ContentOperations.GetParent((ContentElement)current)
                                    ?? (current as FrameworkContentElement)?.Parent
                                    ?? LogicalTreeHelper.GetParent(current),
            ParentLookup.VisualTree => VisualTreeHelper.GetParent(current),
            _ => LogicalTreeHelper.GetParent(current)
        };
    }
}
