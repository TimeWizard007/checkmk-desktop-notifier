using System.Windows;

namespace CheckmkDesktopNotifier.App.Views;

public partial class TakeConfirmWindow : Window
{
    public TakeConfirmWindow(string title, string body, string confirm, string cancel)
    {
        InitializeComponent();
        var content = new DarkConfirmContent
        {
            Title = title,
            Body = body,
            Confirm = confirm,
            Cancel = cancel
        };
        DataContext = content;
        if (!content.ShowCancel)
        {
            ConfirmButton.IsCancel = true;
        }
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}

public sealed class DarkConfirmContent
{
    public required string Title { get; init; }

    public required string Body { get; init; }

    public required string Confirm { get; init; }

    public required string Cancel { get; init; }

    public bool ShowCancel => !string.IsNullOrWhiteSpace(Cancel);
}
