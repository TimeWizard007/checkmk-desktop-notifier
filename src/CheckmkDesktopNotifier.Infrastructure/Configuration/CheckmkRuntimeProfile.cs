namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public static class CheckmkRuntimeProfile
{
    public static bool UseDemoBootstrap(ClientMode mode) => mode == ClientMode.Mock;

    public static bool UseBackgroundPolling(ClientMode mode) => mode == ClientMode.Real;

    public static bool UsePersistentAlertState(ClientMode mode) => mode == ClientMode.Real;
}
