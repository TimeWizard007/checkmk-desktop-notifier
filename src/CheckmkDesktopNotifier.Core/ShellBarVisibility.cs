namespace CheckmkDesktopNotifier.Core;

/// <summary>
/// Shared compact-bar visibility. Gear Hide, tray Open, and tray left-click all use this state.
/// Does not create windows; the shell shows or hides the existing CompactBarWindow.
/// </summary>
public sealed class ShellBarVisibility
{
    public bool IsVisible { get; private set; } = true;

    public void HideToTray() => IsVisible = false;

    public void Restore() => IsVisible = true;

    /// <summary>
    /// Tray icon left-click: visible → hide to tray; hidden → restore.
    /// </summary>
    public void ToggleFromTrayClick() => IsVisible = !IsVisible;
}

public static class AlertSoundAsset
{
    public const string FileName = "notifier.wav";

    public const string RelativePath = "src/CheckmkDesktopNotifier.App/Assets/notifier.wav";

    public const string PackUri = "pack://application:,,,/Assets/notifier.wav";

    public const int SampleRate = 22050;

    public const ushort Channels = 1;

    public const ushort BitsPerSample = 16;
}
