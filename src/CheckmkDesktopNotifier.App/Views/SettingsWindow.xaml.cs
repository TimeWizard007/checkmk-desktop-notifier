using System.Windows;
using CheckmkDesktopNotifier.App.ViewModels;
using Microsoft.Win32;

namespace CheckmkDesktopNotifier.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.ReadSecret = () => SecretBox.Password;
        viewModel.PickWavFile = PickWavFile;
        viewModel.CloseRequested += (_, saved) =>
        {
            DialogResult = saved;
        };
    }

    private string? PickWavFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{((SettingsViewModel)DataContext).Text.SoundWavFilter}|*.wav",
            DefaultExt = ".wav",
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
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
