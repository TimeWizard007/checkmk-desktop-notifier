namespace CheckmkDesktopNotifier.Core.Autostart;

/// <summary>
/// Per-user Start with Windows. OS registration is the source of truth, not preferences.json.
/// </summary>
public sealed class AutostartService
{
    private readonly IAutostartStore _store;
    private readonly IApplicationExecutable _executable;

    public AutostartService(IAutostartStore store, IApplicationExecutable executable)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _executable = executable ?? throw new ArgumentNullException(nameof(executable));
    }

    public bool IsEnabled
    {
        get
        {
            try
            {
                return _store.Read() is not null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public AutostartApplyResult SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                WriteCurrentExecutable();
            }
            else
            {
                _store.Delete();
            }

            return AutostartApplyResult.Ok(IsEnabled);
        }
        catch (Exception)
        {
            return AutostartApplyResult.Failed(SafeIsEnabled());
        }
    }

    /// <summary>
    /// If this app already has a Run entry, refresh it to the current executable path.
    /// Does not create an entry when autostart is off.
    /// </summary>
    public AutostartApplyResult RepairIfRegistered()
    {
        try
        {
            if (_store.Read() is null)
            {
                return AutostartApplyResult.Ok(false);
            }

            WriteCurrentExecutable();
            return AutostartApplyResult.Ok(true);
        }
        catch (Exception)
        {
            return AutostartApplyResult.Failed(SafeIsEnabled());
        }
    }

    public AutostartRegistration? CurrentRegistration()
    {
        try
        {
            return _store.Read();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void WriteCurrentExecutable()
    {
        var command = AutostartCommand.Format(_executable.GetPath());
        if (AutostartCommand.ContainsDisallowedPayload(command))
        {
            throw new InvalidOperationException("Autostart command must not include credentials.");
        }

        _store.Write(new AutostartRegistration(command));
    }

    private bool SafeIsEnabled()
    {
        try
        {
            return _store.Read() is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public sealed class CurrentProcessExecutable : IApplicationExecutable
{
    public string GetPath()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("The current executable path is not available.");
        }

        return path;
    }
}

public sealed class InMemoryAutostartStore : IAutostartStore
{
    private AutostartRegistration? _entry;

    public Exception? ReadFault { get; set; }

    public Exception? WriteFault { get; set; }

    public Exception? DeleteFault { get; set; }

    public int WriteCount { get; private set; }

    public int DeleteCount { get; private set; }

    public AutostartRegistration? Read()
    {
        if (ReadFault is not null)
        {
            throw ReadFault;
        }

        return _entry;
    }

    public void Write(AutostartRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (WriteFault is not null)
        {
            throw WriteFault;
        }

        _entry = registration;
        WriteCount++;
    }

    public void Delete()
    {
        if (DeleteFault is not null)
        {
            throw DeleteFault;
        }

        _entry = null;
        DeleteCount++;
    }
}
