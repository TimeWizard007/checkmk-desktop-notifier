using Avalonia.Controls;
using Avalonia.Input;

namespace CheckmkDesktopNotifier.App.MacOS;

public partial class MainWindow : Window
{
    public MainWindow()
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
