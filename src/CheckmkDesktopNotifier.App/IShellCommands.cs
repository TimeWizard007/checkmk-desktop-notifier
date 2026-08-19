namespace CheckmkDesktopNotifier.App;

public interface IShellCommands
{
    void ShowBar();

    void HideToTray();

    void ToggleBar();

    void ShowSettings();

    void ShowAbout();

    void Exit();
}
