using System.Windows;
using System.Windows.Input;
using CheckmkDesktopNotifier.App.ViewModels;

namespace CheckmkDesktopNotifier.App.Views;

public partial class CompactBarWindow : Window
{
    private Point _dragStart;
    private bool _dragging;

    public CompactBarWindow()
    {
        InitializeComponent();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragging = false;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (_dragging
            || (Math.Abs(position.X - _dragStart.X) <= 4 && Math.Abs(position.Y - _dragStart.Y) <= 4))
        {
            return;
        }

        _dragging = true;
        ReleaseMouseCapture();
        DragMove();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (!_dragging && DataContext is ShellViewModel viewModel)
        {
            viewModel.ToggleExpandedCommand.Execute(null);
        }

        _dragging = false;
    }
}
