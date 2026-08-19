using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CheckmkDesktopNotifier.App.MacOS;

public sealed partial class MacConfirmViewModel : ObservableObject
{
    public MacConfirmViewModel(string title, string body, string confirm, string? cancel)
    {
        Title = title ?? string.Empty;
        Body = body ?? string.Empty;
        Confirm = confirm ?? MacUiCopy.Ok;
        Cancel = cancel ?? string.Empty;
        ShowCancel = !string.IsNullOrWhiteSpace(Cancel);
    }

    public string Title { get; }

    public string Body { get; }

    public string Confirm { get; }

    public string Cancel { get; }

    public bool ShowCancel { get; }

    public bool? Result { get; private set; }

    public Action<bool?>? Close { get; set; }

    [RelayCommand]
    private void Accept()
    {
        Result = true;
        Close?.Invoke(true);
    }

    [RelayCommand]
    private void Dismiss()
    {
        Result = false;
        Close?.Invoke(false);
    }
}

public partial class MacConfirmWindow : Window
{
    public MacConfirmWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    public MacConfirmWindow(MacConfirmViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        viewModel.Close = result => Close(result);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(false);
            e.Handled = true;
        }
    }
}
