using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CheckmkDesktopNotifier.App.Wpf;
using CheckmkDesktopNotifier.Core;

namespace CheckmkDesktopNotifier.App.Views;

public partial class ProblemListWindow : Window
{
    public ProblemListWindow()
    {
        InitializeComponent();
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsFromInteractiveControl(e) || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
    }

    private static bool IsFromInteractiveControl(MouseButtonEventArgs e) =>
        AncestorSearch.IsInside<DependencyObject, ButtonBase>(
            e.OriginalSource as DependencyObject,
            DependencyObjectAncestors.GetParent);
}
