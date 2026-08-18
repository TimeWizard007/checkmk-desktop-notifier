using System.IO;
using CheckmkDesktopNotifier.App.Localization;
using CheckmkDesktopNotifier.Core.Autostart;
using CheckmkDesktopNotifier.Core.Notifications;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CheckmkDesktopNotifier.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly GuiConfigurationService _gui;
    private readonly CheckmkConnectionTester _tester;
    private readonly IMonitoringCoordinator? _coordinator;
    private readonly IAlertSoundService? _sound;
    private readonly IUserPreferences? _preferences;
    private readonly NotificationSoundStore? _sounds;
    private readonly AutostartService? _autostart;
    private readonly bool _requireSecretOnSave;
    private bool _suppressAutostartApply;

    public SettingsViewModel(
        GuiConfigurationService gui,
        CheckmkConnectionTester tester,
        ILocalizationService text,
        IMonitoringCoordinator? coordinator,
        IAlertSoundService? sound = null,
        IUserPreferences? preferences = null,
        NotificationSoundStore? sounds = null,
        AutostartService? autostart = null)
    {
        _gui = gui ?? throw new ArgumentNullException(nameof(gui));
        _tester = tester ?? throw new ArgumentNullException(nameof(tester));
        Text = text ?? throw new ArgumentNullException(nameof(text));
        _coordinator = coordinator;
        _sound = sound;
        _preferences = preferences;
        _sounds = sounds;
        _autostart = autostart;
        _requireSecretOnSave = !gui.HasStoredSecret;
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
        HasStoredSecret = gui.HasStoredSecret;
        RefreshAutostartFromOs();
    }

    public ILocalizationService Text { get; }

    public Func<string>? ReadSecret { get; set; }

    public Func<string?>? PickWavFile { get; set; }

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

    public bool ShowMuteToggle => _preferences is not null;

    public bool IsDefaultSound =>
        _preferences is null || _preferences.SoundSource == NotificationSoundSource.Default;

    public bool IsCustomSound =>
        _preferences?.SoundSource == Core.Notifications.NotificationSoundSource.Custom;

    public string CustomSoundDisplayName =>
        string.IsNullOrWhiteSpace(_preferences?.CustomSoundFileName)
            ? string.Empty
            : _preferences.CustomSoundFileName;

    public bool HasCustomSoundName => !string.IsNullOrWhiteSpace(CustomSoundDisplayName);

    public string VolumePercentText => $"{VolumePercent}%";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumePercentText))]
    private int _volumePercent = 30;

    [ObservableProperty]
    private bool _muteSound;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSoundStatus))]
    private string _soundStatusText = string.Empty;

    public bool HasSoundStatus => !string.IsNullOrWhiteSpace(SoundStatusText);

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAutostartStatus))]
    private string _autostartStatusText = string.Empty;

    public bool HasAutostartStatus => !string.IsNullOrWhiteSpace(AutostartStatusText);

    [ObservableProperty]
    private bool _enableTake;

    [ObservableProperty]
    private string _takeDisplayName = string.Empty;

    private bool _suppressTakeApply;

    public string MuteActionLabel =>
        _preferences is null
            ? string.Empty
            : MuteCommands.MenuHeader(_preferences, Text.MenuMuteSound, Text.MenuUnmuteSound);

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

    [RelayCommand]
    private void TestNotificationSound()
    {
        if (_sound is null)
        {
            return;
        }

        AlertSoundPreview.Play(_sound);
    }

    [RelayCommand]
    private void SelectDefaultSound()
    {
        _preferences?.SetSoundSource(NotificationSoundSource.Default);
        SoundStatusText = string.Empty;
        NotifySoundProperties();
    }

    [RelayCommand]
    private void SelectCustomSound()
    {
        if (_sounds?.TryReadCustomBytes() is not null)
        {
            _preferences?.SetSoundSource(NotificationSoundSource.Custom);
            SoundStatusText = string.Empty;
            NotifySoundProperties();
            return;
        }

        ChooseCustomWav();
    }

    [RelayCommand]
    private void ChooseCustomWav()
    {
        if (_preferences is null || _sounds is null)
        {
            return;
        }

        var path = PickWavFile?.Invoke();
        if (string.IsNullOrWhiteSpace(path))
        {
            NotifySoundProperties();
            return;
        }

        var imported = _sounds.ImportFrom(path);
        if (!imported.Succeeded)
        {
            SoundStatusText = Text.SoundInvalidWav;
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

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_suppressAutostartApply || _autostart is null)
        {
            return;
        }

        var result = _autostart.SetEnabled(value);
        if (!result.Succeeded)
        {
            AutostartStatusText = Text.SettingsAutostartFailed;
            SetStartWithWindowsFromOs(result.IsEnabled);
            return;
        }

        AutostartStatusText = string.Empty;
        if (result.IsEnabled != value)
        {
            SetStartWithWindowsFromOs(result.IsEnabled);
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
        SetStartWithWindowsFromOs(_autostart.IsEnabled);
    }

    private void SetStartWithWindowsFromOs(bool enabled)
    {
        _suppressAutostartApply = true;
        StartWithWindows = enabled;
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
        OnPropertyChanged(nameof(MuteActionLabel));
        NotifySoundProperties();
    }

    private void NotifySoundProperties()
    {
        OnPropertyChanged(nameof(IsDefaultSound));
        OnPropertyChanged(nameof(IsCustomSound));
        OnPropertyChanged(nameof(CustomSoundDisplayName));
        OnPropertyChanged(nameof(HasCustomSoundName));
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
