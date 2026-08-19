using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Autostart;
using CheckmkDesktopNotifier.Core.Navigation;
using CheckmkDesktopNotifier.Core.Notifications;
using CheckmkDesktopNotifier.Core.Threading;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Platform.MacOS;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CheckmkDesktopNotifier.App.MacOS;

public enum MacSettingsSection
{
    General = 0,
    Connection = 1,
    Notifications = 2
}

public sealed partial class MacConnectionViewModel : ObservableObject
{
    private readonly GuiConfigurationService _gui;
    private readonly CheckmkConnectionTester _tester;
    private readonly IMonitoringCoordinator _coordinator;
    private readonly IProblemPoller _poller;
    private readonly IAlertStateService _alerts;
    private readonly IUiThread _uiThread;
    private readonly IUriLauncher _uriLauncher;
    private readonly IAlertSoundService? _sound;
    private readonly IUserPreferences? _preferences;
    private readonly NotificationSoundStore? _sounds;
    private readonly AutostartService? _autostart;
    private readonly bool _requireSecretOnSave;
    private bool _suppressAutostartApply;
    private bool _suppressTakeApply;

    public MacConnectionViewModel(
        GuiConfigurationService gui,
        CheckmkConnectionTester tester,
        IMonitoringCoordinator coordinator,
        IProblemPoller poller,
        IAlertStateService alerts,
        IUiThread uiThread,
        IUriLauncher uriLauncher,
        LoadedConfiguration loaded,
        IAlertSoundService? sound = null,
        IUserPreferences? preferences = null,
        NotificationSoundStore? sounds = null,
        AutostartService? autostart = null)
    {
        _gui = gui ?? throw new ArgumentNullException(nameof(gui));
        _tester = tester ?? throw new ArgumentNullException(nameof(tester));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        _uiThread = uiThread ?? throw new ArgumentNullException(nameof(uiThread));
        _uriLauncher = uriLauncher ?? throw new ArgumentNullException(nameof(uriLauncher));
        ArgumentNullException.ThrowIfNull(loaded);
        _sound = sound;
        _preferences = preferences;
        _sounds = sounds;
        _autostart = autostart;
        _requireSecretOnSave = !gui.HasStoredSecret;
        HasStoredSecret = gui.HasStoredSecret;

        if (_preferences is not null)
        {
            _volumePercent = _preferences.VolumePercent;
            _muteSound = _preferences.MuteSound;
            _enableTake = _preferences.TakeEnabled;
            _takeDisplayName = _preferences.TakeDisplayName
                ?? CheckmkDesktopNotifier.Core.Acknowledgements.TakeDisplayName.SuggestFromUserName();
            _preferences.Changed += (_, _) => RefreshSoundFromPreferences();
        }

        var existing = gui.LoadSettings();
        BaseUrl = existing?.BaseUrl ?? string.Empty;
        Site = existing?.Site ?? string.Empty;
        Username = existing?.Username ?? string.Empty;
        PollIntervalText = (existing?.PollIntervalSeconds ?? CheckmkOptions.DefaultPollIntervalSeconds).ToString();

        StatusText = loaded.NeedsFirstRunSetup
            ? "Not configured"
            : loaded.IsUsableReal
                ? "Connected"
                : loaded.LoadError ?? "Not configured";
        RefreshDiagnostic();
        RefreshAutostartFromOs();
        SelectedSection = loaded.NeedsFirstRunSetup ? MacSettingsSection.Connection : MacSettingsSection.General;
    }

    public bool HasStoredSecret { get; private set; }

    public string TeamHint => MacUiCopy.TeamHint;

    public string LoginItemHint => MacLoginItemCapability.Limitation;

    public Func<Task<string?>>? PickWavFile { get; set; }

    public event EventHandler? Saved;

    public event EventHandler? CloseRequested;

    [ObservableProperty]
    private MacSettingsSection _selectedSection = MacSettingsSection.General;

    public bool IsGeneralSection => SelectedSection == MacSettingsSection.General;

    public bool IsConnectionSection => SelectedSection == MacSettingsSection.Connection;

    public bool IsNotificationsSection => SelectedSection == MacSettingsSection.Notifications;

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private string _site = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _secret = string.Empty;

    [ObservableProperty]
    private string _pollIntervalText = "60";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Not configured";

    [ObservableProperty]
    private string _diagnosticText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumePercentText))]
    private int _volumePercent = 30;

    [ObservableProperty]
    private bool _muteSound;

    [ObservableProperty]
    private bool _startAtLogin;

    [ObservableProperty]
    private string _autostartStatusText = string.Empty;

    [ObservableProperty]
    private bool _enableTake;

    [ObservableProperty]
    private string _takeDisplayName = string.Empty;

    [ObservableProperty]
    private string _soundStatusText = string.Empty;

    public bool CanInteract => !IsBusy;

    public bool HasAutostartStatus => !string.IsNullOrWhiteSpace(AutostartStatusText);

    public bool HasSoundStatus => !string.IsNullOrWhiteSpace(SoundStatusText);

    public string VolumePercentText => VolumePercent + "%";

    public bool IsDefaultSound =>
        _preferences is null || _preferences.SoundSource == NotificationSoundSource.Default;

    public bool IsCustomSound =>
        _preferences?.SoundSource == NotificationSoundSource.Custom;

    public string CustomSoundDisplayName =>
        string.IsNullOrWhiteSpace(_preferences?.CustomSoundFileName)
            ? string.Empty
            : _preferences.CustomSoundFileName;

    public bool HasCustomSoundName => !string.IsNullOrWhiteSpace(CustomSoundDisplayName);

    public void StartListening()
    {
        _poller.StateChanged += OnPollerStateChanged;
        RefreshDiagnostic();
    }

    public void SetStatus(string text) => StatusText = text;

    [RelayCommand]
    private void SelectGeneral() => SelectedSection = MacSettingsSection.General;

    [RelayCommand]
    private void SelectConnection() => SelectedSection = MacSettingsSection.Connection;

    [RelayCommand]
    private void SelectNotifications() => SelectedSection = MacSettingsSection.Notifications;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TryCreateOptions(_requireSecretOnSave, out var options, out var error)
            || options is null)
        {
            StatusText = error ?? "The configuration is invalid.";
            SelectedSection = MacSettingsSection.Connection;
            return;
        }

        IsBusy = true;
        try
        {
            var secret = _gui.ResolveSecret(Secret);
            if (string.IsNullOrWhiteSpace(secret))
            {
                StatusText = "Automation secret is required.";
                SelectedSection = MacSettingsSection.Connection;
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
            Saved?.Invoke(this, EventArgs.Empty);
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
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task ResetAsync()
    {
        IsBusy = true;
        try
        {
            _gui.Reset();
            await _coordinator.ResetPollingAsync().ConfigureAwait(true);
            Secret = string.Empty;
            HasStoredSecret = false;
            StatusText = "Not configured";
            RefreshDiagnostic();
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
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
            SelectedSection = MacSettingsSection.Connection;
            return;
        }

        _uriLauncher.Open(uri);
    }

    [RelayCommand]
    private void TestNotificationSound()
    {
        if (_sound is not null)
        {
            AlertSoundPreview.Play(_sound);
        }
    }

    [RelayCommand]
    private void SelectDefaultSound()
    {
        _preferences?.SetSoundSource(NotificationSoundSource.Default);
        SoundStatusText = string.Empty;
        NotifySoundProperties();
    }

    [RelayCommand]
    private async Task ChooseCustomWavAsync()
    {
        if (_preferences is null || _sounds is null)
        {
            return;
        }

        var path = PickWavFile is null ? null : await PickWavFile().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            NotifySoundProperties();
            return;
        }

        var imported = _sounds.ImportFrom(path);
        if (!imported.Succeeded)
        {
            SoundStatusText = "Select a valid PCM WAV file.";
            NotifySoundProperties();
            return;
        }

        _preferences.SetCustomSoundFileName(imported.FileName);
        _preferences.SetSoundSource(NotificationSoundSource.Custom);
        SoundStatusText = string.Empty;
        NotifySoundProperties();
    }

    [RelayCommand]
    private void RestoreDefaultSound()
    {
        if (_preferences is null)
        {
            return;
        }

        _sounds?.DeleteCustomIfPresent();
        _preferences.SetCustomSoundFileName(null);
        _preferences.SetSoundSource(NotificationSoundSource.Default);
        SoundStatusText = string.Empty;
        NotifySoundProperties();
    }

    partial void OnSelectedSectionChanged(MacSettingsSection value)
    {
        OnPropertyChanged(nameof(IsGeneralSection));
        OnPropertyChanged(nameof(IsConnectionSection));
        OnPropertyChanged(nameof(IsNotificationsSection));
    }

    partial void OnVolumePercentChanged(int value) => _preferences?.SetVolumePercent(value);

    partial void OnMuteSoundChanged(bool value) => _preferences?.SetMuteSound(value);

    partial void OnEnableTakeChanged(bool value)
    {
        if (_suppressTakeApply || _preferences is null)
        {
            return;
        }

        if (value)
        {
            var name = CheckmkDesktopNotifier.Core.Acknowledgements.TakeDisplayName.Normalize(TakeDisplayName);
            if (name is null)
            {
                var suggested = CheckmkDesktopNotifier.Core.Acknowledgements.TakeDisplayName.SuggestFromUserName();
                if (!string.IsNullOrEmpty(suggested))
                {
                    _suppressTakeApply = true;
                    TakeDisplayName = suggested;
                    _suppressTakeApply = false;
                    name = CheckmkDesktopNotifier.Core.Acknowledgements.TakeDisplayName.Normalize(TakeDisplayName);
                }
            }

            if (name is null)
            {
                _suppressTakeApply = true;
                EnableTake = false;
                _suppressTakeApply = false;
                return;
            }

            _preferences.SetTakeDisplayName(name);
        }

        _preferences.SetTakeEnabled(value);
    }

    partial void OnTakeDisplayNameChanged(string value)
    {
        if (_suppressTakeApply || _preferences is null)
        {
            return;
        }

        var name = CheckmkDesktopNotifier.Core.Acknowledgements.TakeDisplayName.Normalize(value);
        if (EnableTake && name is null)
        {
            _suppressTakeApply = true;
            EnableTake = false;
            _suppressTakeApply = false;
            _preferences.SetTakeEnabled(false);
            _preferences.SetTakeDisplayName(null);
            return;
        }

        _preferences.SetTakeDisplayName(name);
    }

    partial void OnStartAtLoginChanged(bool value)
    {
        if (_suppressAutostartApply || _autostart is null)
        {
            return;
        }

        var result = _autostart.SetEnabled(value);
        if (!result.Succeeded)
        {
            AutostartStatusText = "Could not update Start at Login.";
            SetStartAtLoginFromOs(result.IsEnabled);
            return;
        }

        AutostartStatusText = string.Empty;
        if (result.IsEnabled != value)
        {
            SetStartAtLoginFromOs(result.IsEnabled);
        }
    }

    private void RefreshAutostartFromOs()
    {
        if (_autostart is null)
        {
            return;
        }

        _autostart.RepairIfRegistered();
        AutostartStatusText = string.Empty;
        SetStartAtLoginFromOs(_autostart.IsEnabled);
    }

    private void SetStartAtLoginFromOs(bool enabled)
    {
        _suppressAutostartApply = true;
        StartAtLogin = enabled;
        _suppressAutostartApply = false;
    }

    private void RefreshSoundFromPreferences()
    {
        if (_preferences is null)
        {
            return;
        }

        MuteSound = _preferences.MuteSound;
        VolumePercent = _preferences.VolumePercent;
        NotifySoundProperties();
    }

    private void NotifySoundProperties()
    {
        OnPropertyChanged(nameof(IsDefaultSound));
        OnPropertyChanged(nameof(IsCustomSound));
        OnPropertyChanged(nameof(CustomSoundDisplayName));
        OnPropertyChanged(nameof(HasCustomSoundName));
        OnPropertyChanged(nameof(HasSoundStatus));
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
}
