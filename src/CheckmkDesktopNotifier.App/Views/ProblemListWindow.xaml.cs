using System.Windows;
using System.Windows.Input;

namespace CheckmkDesktopNotifier.App.Views;

public partial class ProblemListWindow : Window
{
    public ProblemListWindow()
    {
        InitializeComponent();
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
