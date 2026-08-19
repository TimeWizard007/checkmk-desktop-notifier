using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Navigation;
using CheckmkDesktopNotifier.Core.Threading;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CheckmkDesktopNotifier.App.MacOS;

public sealed partial class MacConnectionViewModel : ObservableObject
{
    private readonly GuiConfigurationService _gui;
    private readonly CheckmkConnectionTester _tester;
    private readonly IMonitoringCoordinator _coordinator;
    private readonly IProblemPoller _poller;
    private readonly IAlertStateService _alerts;
    private readonly IUiThread _uiThread;
    private readonly IUriLauncher _uriLauncher;
    private readonly bool _requireSecretOnSave;

    public MacConnectionViewModel(
        GuiConfigurationService gui,
        CheckmkConnectionTester tester,
        IMonitoringCoordinator coordinator,
        IProblemPoller poller,
        IAlertStateService alerts,
        IUiThread uiThread,
        IUriLauncher uriLauncher,
        LoadedConfiguration loaded)
    {
        _gui = gui ?? throw new ArgumentNullException(nameof(gui));
        _tester = tester ?? throw new ArgumentNullException(nameof(tester));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        _uiThread = uiThread ?? throw new ArgumentNullException(nameof(uiThread));
        _uriLauncher = uriLauncher ?? throw new ArgumentNullException(nameof(uriLauncher));
        ArgumentNullException.ThrowIfNull(loaded);

        _requireSecretOnSave = !gui.HasStoredSecret;
        HasStoredSecret = gui.HasStoredSecret;

        var existing = gui.LoadSettings();
        BaseUrl = existing?.BaseUrl ?? string.Empty;
        Site = existing?.Site ?? string.Empty;
        Username = existing?.Username ?? string.Empty;

        StatusText = loaded.NeedsFirstRunSetup
            ? "Not configured"
            : loaded.IsUsableReal
                ? "Connected"
                : loaded.LoadError ?? "Not configured";
        RefreshDiagnostic();
    }

    public bool HasStoredSecret { get; private set; }

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private string _site = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _secret = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Not configured";

    [ObservableProperty]
    private string _diagnosticText = string.Empty;

    public bool CanInteract => !IsBusy;

    public void StartListening()
    {
        _poller.StateChanged += OnPollerStateChanged;
        RefreshDiagnostic();
    }

    public void SetStatus(string text)
    {
        StatusText = text;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TryCreateOptions(_requireSecretOnSave, out var options, out var error)
            || options is null)
        {
            StatusText = error ?? "The configuration is invalid.";
            return;
        }

        IsBusy = true;
        try
        {
            var secret = _gui.ResolveSecret(Secret);
            if (string.IsNullOrWhiteSpace(secret))
            {
                StatusText = "Automation secret is required.";
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

            _gui.Save(UserSettings.FromOptions(options), string.IsNullOrWhiteSpace(Secret) ? null : secret);
            HasStoredSecret = true;
            await _coordinator.ApplyAsync(options).ConfigureAwait(true);
            StatusText = "Connected";
            Secret = string.Empty;
            RefreshDiagnostic();
        }
        catch (PlatformNotSupportedException)
        {
            StatusText = "Keychain requires macOS. Secrets are not stored in settings files.";
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
    private async Task TestConnectionAsync()
    {
        if (!TryCreateOptions(requireSecret: true, out var options, out var error) || options is null)
        {
            StatusText = error ?? "The configuration is invalid.";
            return;
        }

        IsBusy = true;
        StatusText = "Testing...";
        try
        {
            var result = await _tester.TestAsync(options).ConfigureAwait(true);
            StatusText = result.Status == ConnectionTestStatus.Success
                ? "Connected / success"
                : "Connection error: " + (result.UserMessage ?? "The Checkmk request failed.");
        }
        catch (PlatformNotSupportedException)
        {
            StatusText = "Keychain requires macOS. Secrets are not stored in settings files.";
        }
        catch (Exception)
        {
            StatusText = "Connection error";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenCheckmk()
    {
        if (!MacCheckmkHomeUri.TryCreate(BaseUrl, Site, out var uri) || uri is null)
        {
            StatusText = "Enter a valid https Base URL and site to open Checkmk.";
            return;
        }

        _uriLauncher.Open(uri);
    }

    private void OnPollerStateChanged(object? sender, EventArgs e)
    {
        _uiThread.Post(() =>
        {
            var status = _poller.Status;
            if (status.Kind == ConnectionStatusKind.Error)
            {
                StatusText = "Connection error"
                             + (string.IsNullOrWhiteSpace(status.ErrorSummary)
                                 ? string.Empty
                                 : ": " + status.ErrorSummary);
            }
            else if (status.Kind == ConnectionStatusKind.Connected)
            {
                StatusText = "Connected";
            }

            RefreshDiagnostic();
        });
    }

    private void RefreshDiagnostic()
    {
        DiagnosticText = MacPollSummary.Format(_alerts.GetOpenIncidents(), _poller.Status);
    }

    private bool TryCreateOptions(bool requireSecret, out CheckmkOptions? options, out string? error)
    {
        options = null;
        error = null;
        try
        {
            var secret = _gui.ResolveSecret(Secret);
            options = GuiSettingsValidator.CreateOptions(
                BaseUrl,
                Site,
                Username,
                secret,
                CheckmkOptions.DefaultPollIntervalSeconds.ToString(),
                requireSecret);
            return true;
        }
        catch (CheckmkOptionsValidationException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
