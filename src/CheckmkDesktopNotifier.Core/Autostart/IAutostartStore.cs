namespace CheckmkDesktopNotifier.Core.Autostart;

public sealed record AutostartRegistration(string CommandLine);

public interface IAutostartStore
{
    AutostartRegistration? Read();

    void Write(AutostartRegistration registration);

    void Delete();
}

public interface IApplicationExecutable
{
    string GetPath();
}

public sealed class AutostartApplyResult
{
    private AutostartApplyResult(bool succeeded, bool isEnabled)
    {
        Succeeded = succeeded;
        IsEnabled = isEnabled;
    }

    public bool Succeeded { get; }

    public bool IsEnabled { get; }

    public static AutostartApplyResult Ok(bool isEnabled) => new(true, isEnabled);

    public static AutostartApplyResult Failed(bool isEnabled) => new(false, isEnabled);
}
