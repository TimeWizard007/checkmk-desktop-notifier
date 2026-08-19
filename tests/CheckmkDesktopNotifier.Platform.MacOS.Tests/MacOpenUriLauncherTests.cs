using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.Platform.MacOS.Tests;

public sealed class MacOpenUriLauncherTests
{
    [Fact]
    public void BuildArguments_uses_open_and_absolute_uri_only()
    {
        var uri = new Uri("https://checkmk.example.invalid/site/check_mk/");
        var arguments = MacOpenCommand.BuildArguments(uri);
        Assert.Equal("/usr/bin/open", MacOpenCommand.Executable);
        Assert.Equal(new[] { uri.AbsoluteUri }, arguments.ToArray());
        Assert.DoesNotContain("secret", arguments[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", arguments[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildArguments_rejects_embedded_credentials()
    {
        var uri = new Uri("https://user:pass@checkmk.example.invalid/site/");
        Assert.Throws<ArgumentException>(() => MacOpenCommand.BuildArguments(uri));
    }

    [Fact]
    public void Open_starts_usr_bin_open_with_uri_and_does_not_throw_on_failure()
    {
        var starter = new RecordingProcessStarter { ThrowOnStart = true };
        var launcher = new MacOpenUriLauncher(starter);
        var uri = new Uri("https://checkmk.example.invalid/mysite/check_mk/");
        launcher.Open(uri);
        Assert.Equal("/usr/bin/open", starter.FileName);
        Assert.Equal(new[] { uri.AbsoluteUri }, starter.Arguments!.ToArray());
    }

    [Fact]
    public void Open_does_not_start_process_when_arguments_cannot_be_built()
    {
        var starter = new RecordingProcessStarter();
        var launcher = new MacOpenUriLauncher(starter);
        launcher.Open(new Uri("https://user:secret@checkmk.example.invalid/"));
        Assert.Null(starter.FileName);
    }

    private sealed class RecordingProcessStarter : IProcessStarter
    {
        public string? FileName { get; private set; }

        public IReadOnlyList<string>? Arguments { get; private set; }

        public bool ThrowOnStart { get; init; }

        public void Start(string fileName, IReadOnlyList<string> arguments)
        {
            FileName = fileName;
            Arguments = arguments;
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("open failed");
            }
        }
    }
}
