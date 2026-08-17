using CheckmkDesktopNotifier.App.Localization;
using CheckmkDesktopNotifier.Core;
using CommunityToolkit.Mvvm.Input;

namespace CheckmkDesktopNotifier.App.ViewModels;

public sealed class AboutViewModel
{
    private readonly IUriLauncher _launcher;

    public AboutViewModel(ILocalizationService text, string version, IUriLauncher launcher)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Version = string.IsNullOrWhiteSpace(version) ? "unknown" : version;
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        OpenRepositoryCommand = new RelayCommand(OpenRepository);
    }

    public ILocalizationService Text { get; }

    public string ProductName => ProductInfo.ProductName;

    public string Author => ProductInfo.Author;

    public string Version { get; }

    public string RepositoryUrl => ProductInfo.RepositoryUrl;

    public IRelayCommand OpenRepositoryCommand { get; }

    private void OpenRepository() => _launcher.Open(ProductInfo.Repository);
}
