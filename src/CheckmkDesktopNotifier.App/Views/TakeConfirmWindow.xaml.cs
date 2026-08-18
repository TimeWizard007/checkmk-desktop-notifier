using System.Windows;
using CheckmkDesktopNotifier.App.Localization;

namespace CheckmkDesktopNotifier.App.Views;

public partial class TakeConfirmWindow : Window
{
    public TakeConfirmWindow(ILocalizationService text)
    {
        InitializeComponent();
        DataContext = text ?? throw new ArgumentNullException(nameof(text));
    }

    private void OnTakeClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
