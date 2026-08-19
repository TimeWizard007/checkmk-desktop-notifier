using Avalonia.Controls;
using Avalonia.Input;

namespace CheckmkDesktopNotifier.App.MacOS;

public partial class ProblemPanelWindow : Window
{
    public ProblemPanelWindow()
    {
        InitializeComponent();
        KeyDown += OnEscape;
    }

    private void OnEscape(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }
}
