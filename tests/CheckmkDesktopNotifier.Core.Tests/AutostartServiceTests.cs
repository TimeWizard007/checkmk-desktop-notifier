using CheckmkDesktopNotifier.Core.Autostart;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class AutostartServiceTests
{
    private const string DefaultPath = @"C:\Apps\CheckmkDesktopNotifier.exe";
    private const string SpacedPath = @"C:\Program Files\Checkmk Desktop Notifier\CheckmkDesktopNotifier.exe";

    [Fact]
    public void Default_is_disabled()
    {
        var service = Create();
        Assert.False(service.IsEnabled);
        Assert.Null(service.CurrentRegistration());
    }

    [Fact]
    public void Enable_creates_quoted_per_user_command()
    {
        var store = new InMemoryAutostartStore();
        var service = Create(store, DefaultPath);
        var result = service.SetEnabled(true);

        Assert.True(result.Succeeded);
        Assert.True(result.IsEnabled);
        Assert.True(service.IsEnabled);
        var command = Assert.Single(new[] { store.Read()!.CommandLine });
        Assert.Equal(AutostartCommand.Format(DefaultPath), command);
        Assert.Equal(DefaultPath, AutostartCommand.Unquote(command));
        Assert.False(AutostartCommand.ContainsDisallowedPayload(command));
    }

    [Fact]
    public void Disable_removes_only_this_entry()
    {
        var store = new InMemoryAutostartStore();
        var service = Create(store);
        service.SetEnabled(true);
        var result = service.SetEnabled(false);

        Assert.True(result.Succeeded);
        Assert.False(service.IsEnabled);
        Assert.Null(store.Read());
        Assert.Equal(1, store.DeleteCount);
    }

    [Fact]
    public void Path_with_spaces_is_quoted()
    {
        var store = new InMemoryAutostartStore();
        var service = Create(store, SpacedPath);
        service.SetEnabled(true);

        var command = store.Read()!.CommandLine;
        Assert.StartsWith("\"", command, StringComparison.Ordinal);
        Assert.EndsWith("\"", command, StringComparison.Ordinal);
        Assert.Equal($"\"{SpacedPath}\"", command);
        Assert.DoesNotContain("Authorization", command, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", command, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", command, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Command_never_includes_secrets_or_extra_arguments()
    {
        var command = AutostartCommand.Format(SpacedPath);
        Assert.Equal($"\"{SpacedPath}\"", command);
        Assert.False(command.Contains(' ', StringComparison.Ordinal) && command.Contains(" --", StringComparison.Ordinal));
        Assert.False(AutostartCommand.ContainsDisallowedPayload(command));
    }

    [Fact]
    public void Checkbox_source_is_actual_registration_state()
    {
        var store = new InMemoryAutostartStore();
        var service = Create(store);
        Assert.False(service.IsEnabled);
        service.SetEnabled(true);
        Assert.True(service.IsEnabled);
        store.Delete();
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void External_deletion_is_reflected()
    {
        var store = new InMemoryAutostartStore();
        var service = Create(store);
        service.SetEnabled(true);
        store.Delete();
        Assert.False(service.IsEnabled);
        Assert.Null(service.CurrentRegistration());
    }

    [Fact]
    public void Re_enable_repairs_missing_entry()
    {
        var store = new InMemoryAutostartStore();
        var service = Create(store, DefaultPath);
        service.SetEnabled(true);
        store.Delete();
        var result = service.SetEnabled(true);
        Assert.True(result.Succeeded);
        Assert.Equal(AutostartCommand.Format(DefaultPath), store.Read()!.CommandLine);
    }

    [Fact]
    public void Changed_executable_path_updates_entry()
    {
        var store = new InMemoryAutostartStore();
        Create(store, DefaultPath).SetEnabled(true);
        var moved = Create(store, SpacedPath);
        var repaired = moved.RepairIfRegistered();

        Assert.True(repaired.Succeeded);
        Assert.True(repaired.IsEnabled);
        Assert.Equal(AutostartCommand.Format(SpacedPath), store.Read()!.CommandLine);
    }

    [Fact]
    public void Write_failure_does_not_throw()
    {
        var store = new InMemoryAutostartStore
        {
            WriteFault = new IOException("access denied")
        };
        var service = Create(store);
        var result = service.SetEnabled(true);
        Assert.False(result.Succeeded);
        Assert.False(result.IsEnabled);
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void Repair_failure_does_not_throw()
    {
        var store = new InMemoryAutostartStore();
        Create(store).SetEnabled(true);
        store.WriteFault = new UnauthorizedAccessException("denied");
        var result = Create(store).RepairIfRegistered();
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Location_is_hkcu_run_not_hklm()
    {
        Assert.Equal("HKEY_CURRENT_USER", AutostartCommand.Hive);
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", AutostartCommand.SubKey);
        Assert.Equal("CheckmkDesktopNotifier", AutostartCommand.ValueName);
        Assert.DoesNotContain("HKEY_LOCAL_MACHINE", AutostartCommand.Hive, StringComparison.Ordinal);
        Assert.DoesNotContain("LocalMachine", AutostartCommand.SubKey, StringComparison.Ordinal);
    }

    [Fact]
    public void Autostart_does_not_require_mock_or_checkmk_mode()
    {
        var service = Create();
        var result = service.SetEnabled(true);
        Assert.True(result.Succeeded);
        Assert.True(service.IsEnabled);
    }

    private static AutostartService Create(InMemoryAutostartStore? store = null, string? path = null) =>
        new(store ?? new InMemoryAutostartStore(), new StubExecutable(path ?? DefaultPath));

    private sealed class StubExecutable : IApplicationExecutable
    {
        private readonly string _path;

        public StubExecutable(string path) => _path = path;

        public string GetPath() => _path;
    }
}
