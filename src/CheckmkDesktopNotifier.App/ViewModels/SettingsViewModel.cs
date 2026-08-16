using System.IO;
using CheckmkDesktopNotifier.App.Localization;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CheckmkDesktopNotifier.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly GuiConfigurationService _gui;
    private readonly CheckmkConnectionTester _tester;
    private readonly IMonitoringCoordinator? _coordinator;
    private readonly bool _requireSecretOnSave;

    public SettingsViewModel(
        GuiConfigurationService gui,
        CheckmkConnectionTester tester,
        ILocalizationService text,
        IMonitoringCoordinator? coordinator)
    {
        _gui = gui ?? throw new ArgumentNullException(nameof(gui));
        _tester = tester ?? throw new ArgumentNullException(nameof(tester));
        Text = text ?? throw new ArgumentNullException(nameof(text));
        _coordinator = coordinator;
        _requireSecretOnSave = !gui.HasStoredSecret;

        var existing = gui.LoadSettings();
        BaseUrl = existing?.BaseUrl ?? string.Empty;
        Site = existing?.Site ?? string.Empty;
        Username = existing?.Username ?? string.Empty;
        PollIntervalText = (existing?.PollIntervalSeconds ?? CheckmkOptions.DefaultPollIntervalSeconds).ToString();
        HasStoredSecret = gui.HasStoredSecret;
    }

    public ILocalizationService Text { get; }

    public Func<string>? ReadSecret { get; set; }

    public event EventHandler<bool>? CloseRequested;

    public bool HasStoredSecret { get; }

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private string _site = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _pollIntervalText = "60";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    private bool _isBusy;

    public bool CanInteract => !IsBusy;

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (!TryCreateOptions(requireSecret: true, out var options, out var error) || options is null)
        {
            StatusText = error ?? Text.SettingsValidationFailed;
            return;
        }

        IsBusy = true;
        StatusText = string.Empty;
        try
        {
            var result = await _tester.TestAsync(options).ConfigureAwait(true);
            StatusText = FormatTestResult(result);
        }
        catch (Exception)
        {
            StatusText = Text.TestUnavailable;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TryCreateOptions(_requireSecretOnSave, out var options, out var error) || options is null)
        {
            StatusText = error ?? Text.SettingsValidationFailed;
            return;
        }

        IsBusy = true;
        try
        {
            var typedSecret = ReadSecret?.Invoke();
            var secret = _gui.ResolveSecret(typedSecret);
            if (string.IsNullOrWhiteSpace(secret))
            {
                StatusText = Text.SettingsValidationFailed;
                return;
            }

            options = new CheckmkOptions
            {
                Mode = ClientMode.Real,
                BaseUrl = options.BaseUrl,
                Site = options.Site,
                Username = options.Username,
                Secret = secret,
                PollIntervalSeconds = options.PollIntervalSeconds
            };

            _gui.Save(UserSettings.FromOptions(options), string.IsNullOrWhiteSpace(typedSecret) ? null : secret);
            if (_coordinator is not null)
            {
                await _coordinator.ApplyAsync(options).ConfigureAwait(true);
            }

            CloseRequested?.Invoke(this, true);
        }
        catch (Exception ex) when (ex is CheckmkOptionsValidationException or InvalidOperationException or IOException)
        {
            StatusText = ConnectionTestResult.Sanitize(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    [RelayCommand]
    private async Task ResetAsync()
    {
        IsBusy = true;
        try
        {
            _gui.Reset();
            if (_coordinator is not null)
            {
                await _coordinator.ResetPollingAsync().ConfigureAwait(true);
            }

            CloseRequested?.Invoke(this, true);
        }
        catch (Exception)
        {
            StatusText = Text.TestUnavailable;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryCreateOptions(bool requireSecret, out CheckmkOptions? options, out string? error)
    {
        options = null;
        error = null;
        try
        {
            var typedSecret = ReadSecret?.Invoke();
            var secret = requireSecret ? _gui.ResolveSecret(typedSecret) : _gui.ResolveSecret(typedSecret);
            options = GuiSettingsValidator.CreateOptions(
                BaseUrl,
                Site,
                Username,
                secret,
                PollIntervalText,
                requireSecret);
            return true;
        }
        catch (CheckmkOptionsValidationException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private string FormatTestResult(ConnectionTestResult result)
    {
        var headline = result.Status switch
        {
            ConnectionTestStatus.Success => Text.TestSuccess,
            ConnectionTestStatus.Unauthorized => Text.TestUnauthorized,
            ConnectionTestStatus.Forbidden => Text.TestForbidden,
            ConnectionTestStatus.Unreachable => Text.TestUnreachable,
            ConnectionTestStatus.Timeout => Text.TestTimeout,
            ConnectionTestStatus.TlsError => Text.TestTls,
            ConnectionTestStatus.InvalidConfiguration => Text.TestInvalidConfiguration,
            ConnectionTestStatus.UnexpectedApiResponse => Text.TestUnexpectedApi,
            _ => Text.TestUnavailable
        };

        if (result.Status != ConnectionTestStatus.Success)
        {
            return headline;
        }

        return $"{headline}{Environment.NewLine}{Text.TestServicesReachable}{Environment.NewLine}{Text.TestHostsReachable}";
    }
}
