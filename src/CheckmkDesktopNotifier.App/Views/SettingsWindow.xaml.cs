using System.Windows;
using CheckmkDesktopNotifier.App.ViewModels;

namespace CheckmkDesktopNotifier.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.ReadSecret = () => SecretBox.Password;
        viewModel.CloseRequested += (_, saved) =>
        {
            DialogResult = saved;
        };
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            viewModel.Text.SettingsResetConfirm,
            viewModel.Text.SettingsTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        viewModel.ResetCommand.Execute(null);
    }
}
